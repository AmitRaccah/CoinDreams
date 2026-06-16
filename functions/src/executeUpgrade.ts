import { onCall, HttpsError, CallableRequest } from "firebase-functions/v2/https";
import { logger } from "firebase-functions/v2";
import { getFirestore } from "firebase-admin/firestore";

import {
    AuthoritativeVillageUpgradeRequest,
    AuthoritativeVillageUpgradeResult,
    BuildingUpgradeResult,
    BuildingUpgradeStatus,
    PlayerProfileSnapshot,
} from "./types";

import {
    loadSnapshot,
    hasProcessedImpact,
    stampProcessedImpact,
    applyEnergyRegen,
    stampUpdate,
    nowUtcTicks,
} from "./internal/playerSnapshotHelpers";

/**
 * executeUpgrade — server-authoritative village building upgrade.
 *
 * Ported from `Assets/Scripts/Domain/Village/AuthoritativeVillageUpgradeEngine.cs`.
 *
 * Order of operations inside the transaction:
 *   1. Load /players/{uid} snapshot via loadSnapshot().
 *   2. Reject duplicate upgradeRequestId via processedImpactIds (return Success
 *      with current state to mirror C# AlreadyApplied semantics).
 *   3. Resolve buildingIndex from buildingId or useBuildingIndex.
 *   4. Pad villageLevels to catalog size (matches C# VillageProgressState.EnsureCapacity).
 *   5. Look up upgradeCostsByBuilding[index][currentLevel]; reject MaxLevel
 *      / NotEnoughCoins / InvalidConfiguration.
 *   6. Apply energy regen (parity with C# CreateSnapshotWithStamp -> ApplyTimeBasedRegen),
 *      subtract coins, increment villageLevels[index], stamp processedImpactId,
 *      bump updatedAtUtcTicks.
 *   7. Commit and return AuthoritativeVillageUpgradeResult.
 */
export const executeUpgrade = onCall<
    AuthoritativeVillageUpgradeRequest,
    Promise<AuthoritativeVillageUpgradeResult>
>(
    { region: "us-central1" },
    async (
        request: CallableRequest<AuthoritativeVillageUpgradeRequest>
    ): Promise<AuthoritativeVillageUpgradeResult> => {
        if (!request.auth) {
            throw new HttpsError("unauthenticated", "Sign-in required.");
        }
        const callerUid = request.auth.uid;

        const payload = request.data;
        if (!payload || typeof payload !== "object") {
            throw new HttpsError("invalid-argument", "Request body is missing.");
        }
        if (
            !payload.upgradeRequestId ||
            typeof payload.upgradeRequestId !== "string" ||
            payload.upgradeRequestId.trim().length === 0
        ) {
            throw new HttpsError("invalid-argument", "upgradeRequestId is required.");
        }
        if (!payload.catalog || !Array.isArray(payload.catalog.buildingIds)) {
            throw new HttpsError("invalid-argument", "catalog is required.");
        }
        if (!Array.isArray(payload.catalog.upgradeCostsByBuilding)) {
            throw new HttpsError("invalid-argument", "catalog.upgradeCostsByBuilding is required.");
        }
        if (
            payload.catalog.buildingIds.length !==
            payload.catalog.upgradeCostsByBuilding.length
        ) {
            throw new HttpsError(
                "invalid-argument",
                "catalog.buildingIds length must equal catalog.upgradeCostsByBuilding length."
            );
        }

        logger.info("executeUpgrade invoked", {
            uid: callerUid,
            upgradeRequestId: payload.upgradeRequestId,
            useBuildingIndex: payload.useBuildingIndex,
            buildingId: payload.buildingId,
            buildingIndex: payload.buildingIndex,
        });

        const db = getFirestore();

        try {
            const result = await db.runTransaction(
                async (tx): Promise<AuthoritativeVillageUpgradeResult> => {
                    // 1. Load snapshot (single read before any write).
                    const playerData = await loadSnapshot(tx, db, callerUid);
                    const snapshot = playerData.snapshot;

                    // 2. Idempotency check — duplicate upgradeRequestId.
                    if (hasProcessedImpact(snapshot, payload.upgradeRequestId)) {
                        return alreadyProcessedResult(snapshot, payload);
                    }

                    // 3. Resolve buildingIndex.
                    const buildingCount = payload.catalog.buildingIds.length;
                    const buildingIndex = payload.useBuildingIndex
                        ? payload.buildingIndex
                        : payload.catalog.buildingIds.indexOf(payload.buildingId);
                    if (
                        buildingIndex < 0 ||
                        buildingIndex >= buildingCount
                    ) {
                        return invalidConfigResult(snapshot, buildingIndex);
                    }

                    // 4. Pad villageLevels to catalog length, then read currentLevel.
                    ensureVillageCapacity(snapshot, buildingCount);
                    const currentLevel = snapshot.villageLevels[buildingIndex] ?? 0;

                    const costs = payload.catalog.upgradeCostsByBuilding[buildingIndex] ?? [];
                    if (currentLevel >= costs.length) {
                        return maxLevelResult(snapshot, buildingIndex, currentLevel);
                    }

                    // 5. Cost check.
                    const cost = costs[currentLevel];
                    if (typeof cost !== "number" || cost < 0) {
                        return invalidConfigResult(snapshot, buildingIndex);
                    }
                    if (snapshot.coins < cost) {
                        return notEnoughCoinsResult(snapshot, buildingIndex, currentLevel);
                    }

                    // 6. Apply mutation: regen parity, spend, level up, stamp.
                    const now = nowUtcTicks();
                    applyEnergyRegen(snapshot, now);
                    snapshot.coins -= cost;
                    const newLevel = currentLevel + 1;
                    snapshot.villageLevels[buildingIndex] = newLevel;
                    stampProcessedImpact(snapshot, payload.upgradeRequestId);
                    stampUpdate(snapshot, now);

                    // 7. Commit.
                    tx.set(playerData.ref, snapshot);

                    return successResult(snapshot, buildingIndex, newLevel, cost);
                }
            );
            return result;
        } catch (err) {
            if (err instanceof HttpsError) {
                throw err;
            }
            logger.error("executeUpgrade failed", {
                uid: callerUid,
                upgradeRequestId: payload.upgradeRequestId,
                err,
            });
            const result: AuthoritativeVillageUpgradeResult = {
                upgradeResult: {
                    status: BuildingUpgradeStatus.UnexpectedError,
                    buildingIndex: -1,
                    newLevel: -1,
                    coinsSpent: 0,
                    message: "Internal server error.",
                },
                snapshot: null,
                message: "Internal server error.",
            };
            return result;
        }
    }
);

// ---------------------------------------------------------------------------
// Helpers (scoped to executeUpgrade.ts — do NOT promote to shared helpers).
// ---------------------------------------------------------------------------

/**
 * Pads `snapshot.villageLevels` with zeros up to `count` entries.
 * Matches the grow-only semantics of C# `VillageProgressState.EnsureCapacity`.
 */
function ensureVillageCapacity(snapshot: PlayerProfileSnapshot, count: number): void {
    if (count <= 0) {
        return;
    }
    if (!Array.isArray(snapshot.villageLevels)) {
        snapshot.villageLevels = [];
    }
    while (snapshot.villageLevels.length < count) {
        snapshot.villageLevels.push(0);
    }
}

function buildUpgradeResult(
    status: BuildingUpgradeStatus,
    buildingIndex: number,
    newLevel: number,
    coinsSpent: number,
    message: string
): BuildingUpgradeResult {
    return {
        status,
        buildingIndex,
        newLevel,
        coinsSpent,
        message,
    };
}

function successResult(
    snapshot: PlayerProfileSnapshot,
    buildingIndex: number,
    newLevel: number,
    cost: number
): AuthoritativeVillageUpgradeResult {
    const upgradeResult = buildUpgradeResult(
        BuildingUpgradeStatus.Success,
        buildingIndex,
        newLevel,
        cost,
        ""
    );
    return {
        upgradeResult,
        snapshot,
        message: "",
    };
}

function notEnoughCoinsResult(
    snapshot: PlayerProfileSnapshot,
    buildingIndex: number,
    currentLevel: number
): AuthoritativeVillageUpgradeResult {
    const message = "Not enough coins for upgrade.";
    const upgradeResult = buildUpgradeResult(
        BuildingUpgradeStatus.NotEnoughCoins,
        buildingIndex,
        currentLevel,
        0,
        message
    );
    return {
        upgradeResult,
        snapshot,
        message,
    };
}

function maxLevelResult(
    snapshot: PlayerProfileSnapshot,
    buildingIndex: number,
    currentLevel: number
): AuthoritativeVillageUpgradeResult {
    const message = "Building is at maximum level.";
    const upgradeResult = buildUpgradeResult(
        BuildingUpgradeStatus.MaxLevel,
        buildingIndex,
        currentLevel,
        0,
        message
    );
    return {
        upgradeResult,
        snapshot,
        message,
    };
}

function invalidConfigResult(
    snapshot: PlayerProfileSnapshot,
    buildingIndex: number
): AuthoritativeVillageUpgradeResult {
    const message = "Invalid upgrade configuration.";
    const upgradeResult = buildUpgradeResult(
        BuildingUpgradeStatus.InvalidConfiguration,
        buildingIndex,
        -1,
        0,
        message
    );
    return {
        upgradeResult,
        snapshot,
        message,
    };
}

/**
 * Idempotent replay path. The C# engine returns `BuildingUpgradeResult.AlreadyApplied`
 * which has no equivalent in the wire-format TS enum, so we collapse it to
 * Success carrying the *current* persisted state — matching the spec's
 * "AlreadyApplied semantics" note. We do NOT mutate the snapshot here; the
 * upgrade was already applied in a previous transaction.
 */
function alreadyProcessedResult(
    snapshot: PlayerProfileSnapshot,
    payload: AuthoritativeVillageUpgradeRequest
): AuthoritativeVillageUpgradeResult {
    const buildingCount = payload.catalog.buildingIds.length;
    let buildingIndex = payload.useBuildingIndex
        ? payload.buildingIndex
        : payload.catalog.buildingIds.indexOf(payload.buildingId);
    if (buildingIndex < 0 || buildingIndex >= buildingCount) {
        buildingIndex = -1;
    }
    const currentLevel = buildingIndex >= 0
        ? (snapshot.villageLevels[buildingIndex] ?? 0)
        : 0;
    const upgradeResult = buildUpgradeResult(
        BuildingUpgradeStatus.Success,
        buildingIndex,
        currentLevel,
        0,
        "Upgrade already processed."
    );
    return {
        upgradeResult,
        snapshot,
        message: "Upgrade already processed.",
    };
}
