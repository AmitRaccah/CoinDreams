import { onCall, HttpsError, CallableRequest } from "firebase-functions/v2/https";
import { logger } from "firebase-functions/v2";
import { getFirestore } from "firebase-admin/firestore";

import {
    AuthoritativeStageAdvanceRequest,
    AuthoritativeStageAdvanceResult,
    AuthoritativeVillageUpgradeCatalogData,
    PlayerProfileSnapshot,
    StageAdvanceStatus,
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
 * advanceStage — server-authoritative "next stage" transition.
 *
 * Pre-condition: EVERY building in the catalog must already be at max level
 * (current >= upgradeCostsByBuilding[i].length). The client detects this to
 * show the stage-complete UI, but the server re-validates here — the client
 * check is advisory only, never trusted.
 *
 * On success it resets villageLevels to all-zeros (the player rebuilds the
 * SAME village next stage) and increments currentStage. Idempotent on
 * stageAdvanceId so a retried call never double-advances.
 *
 * Reuses the shared snapshot helpers (loadSnapshot / stampProcessedImpact /
 * stampUpdate / applyEnergyRegen) — the only stage-specific logic is the
 * all-maxed check and the reset, so there is no duplicated bookkeeping.
 */
export const advanceStage = onCall<
    AuthoritativeStageAdvanceRequest,
    Promise<AuthoritativeStageAdvanceResult>
>(
    { region: "us-central1" },
    async (
        request: CallableRequest<AuthoritativeStageAdvanceRequest>
    ): Promise<AuthoritativeStageAdvanceResult> => {
        if (!request.auth) {
            throw new HttpsError("unauthenticated", "Sign-in required.");
        }
        const callerUid = request.auth.uid;

        const payload = request.data;
        if (!payload || typeof payload !== "object") {
            throw new HttpsError("invalid-argument", "Request body is missing.");
        }
        if (
            !payload.stageAdvanceId ||
            typeof payload.stageAdvanceId !== "string" ||
            payload.stageAdvanceId.trim().length === 0
        ) {
            throw new HttpsError("invalid-argument", "stageAdvanceId is required.");
        }
        if (
            !payload.catalog ||
            !Array.isArray(payload.catalog.buildingIds) ||
            !Array.isArray(payload.catalog.upgradeCostsByBuilding)
        ) {
            throw new HttpsError("invalid-argument", "catalog is required.");
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
        if (payload.catalog.buildingIds.length === 0) {
            throw new HttpsError("invalid-argument", "catalog has no buildings.");
        }

        logger.info("advanceStage invoked", {
            uid: callerUid,
            stageAdvanceId: payload.stageAdvanceId,
            buildingCount: payload.catalog.buildingIds.length,
        });

        const db = getFirestore();

        try {
            const result = await db.runTransaction(
                async (tx): Promise<AuthoritativeStageAdvanceResult> => {
                    const playerData = await loadSnapshot(tx, db, callerUid);
                    const snapshot = playerData.snapshot;

                    // Idempotency — a retried advance returns the current state.
                    if (hasProcessedImpact(snapshot, payload.stageAdvanceId)) {
                        return alreadyProcessedResult(snapshot);
                    }

                    // Gate: the stage is only clearable when EVERY building is maxed.
                    if (!areAllBuildingsMaxed(snapshot, payload.catalog)) {
                        logger.warn("advanceStage rejected — not all buildings maxed", {
                            uid: callerUid,
                            villageLevels: snapshot.villageLevels,
                        });
                        return notAllMaxedResult(snapshot);
                    }

                    // Apply: regen parity, reset the village, bump the stage, stamp.
                    const now = nowUtcTicks();
                    applyEnergyRegen(snapshot, now);
                    snapshot.villageLevels = new Array(payload.catalog.buildingIds.length).fill(0);
                    snapshot.currentStage = (snapshot.currentStage ?? 0) + 1;
                    stampProcessedImpact(snapshot, payload.stageAdvanceId);
                    stampUpdate(snapshot, now);

                    tx.set(playerData.ref, snapshot);

                    return successResult(snapshot);
                }
            );
            return result;
        } catch (err) {
            if (err instanceof HttpsError) {
                throw err;
            }
            logger.error("advanceStage failed", {
                uid: callerUid,
                stageAdvanceId: payload.stageAdvanceId,
                err,
            });
            return {
                status: StageAdvanceStatus.UnexpectedError,
                snapshot: null,
                newStage: -1,
                message: "Internal server error.",
            };
        }
    }
);

// ---------------------------------------------------------------------------
// Helpers (scoped to advanceStage.ts).
// ---------------------------------------------------------------------------

/**
 * True only when every building has reached its max level. Max level for
 * building i = upgradeCostsByBuilding[i].length (each cost is one step). A
 * building with no steps (length 0) is trivially maxed at level 0. Missing
 * villageLevels entries read as level 0, so a short array fails the gate.
 */
function areAllBuildingsMaxed(
    snapshot: PlayerProfileSnapshot,
    catalog: AuthoritativeVillageUpgradeCatalogData
): boolean {
    const costs = catalog.upgradeCostsByBuilding;
    for (let i = 0; i < costs.length; i++) {
        const maxLevel = Array.isArray(costs[i]) ? costs[i].length : 0;
        const currentLevel = snapshot.villageLevels[i] ?? 0;
        if (currentLevel < maxLevel) {
            return false;
        }
    }
    return true;
}

function successResult(snapshot: PlayerProfileSnapshot): AuthoritativeStageAdvanceResult {
    return {
        status: StageAdvanceStatus.Success,
        snapshot,
        newStage: snapshot.currentStage,
        message: "",
    };
}

function notAllMaxedResult(snapshot: PlayerProfileSnapshot): AuthoritativeStageAdvanceResult {
    return {
        status: StageAdvanceStatus.NotAllBuildingsMaxed,
        snapshot,
        newStage: snapshot.currentStage ?? 0,
        message: "Not all buildings are at max level.",
    };
}

/**
 * Idempotent replay path — the advance was already applied in a prior
 * transaction. Returns Success carrying the current (already-advanced) stage;
 * we do NOT mutate the snapshot here.
 */
function alreadyProcessedResult(snapshot: PlayerProfileSnapshot): AuthoritativeStageAdvanceResult {
    return {
        status: StageAdvanceStatus.Success,
        snapshot,
        newStage: snapshot.currentStage ?? 0,
        message: "Stage advance already processed.",
    };
}
