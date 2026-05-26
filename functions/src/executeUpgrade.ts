import { onCall, HttpsError, CallableRequest } from "firebase-functions/v2/https";
import { logger } from "firebase-functions/v2";
import { getFirestore } from "firebase-admin/firestore";

import {
    AuthoritativeVillageUpgradeRequest,
    AuthoritativeVillageUpgradeResult,
    BuildingUpgradeStatus,
} from "./types";

/**
 * executeUpgrade — server-authoritative village building upgrade.
 *
 * TODO: Translate engine logic from
 *       `Assets/Scripts/Domain/Village/AuthoritativeVillageUpgradeEngine.cs`.
 *       Until that port lands, this callable returns HttpsError("unimplemented").
 *
 * Implementation checklist:
 *   1. Validate request shape (upgradeRequestId non-empty, catalog non-empty,
 *      buildingIndex within bounds OR buildingId resolvable).
 *   2. Open a Firestore transaction on `players/{uid}`.
 *   3. Reject duplicate upgradeRequestId via processedImpactIds.
 *   4. Resolve target buildingIndex from buildingId/buildingIndex flags.
 *   5. Look up `upgradeCostsByBuilding[index][currentLevel]`; reject if
 *      MaxLevel or NotEnoughCoins.
 *   6. Subtract coins, increment villageLevels[index], bump revision,
 *      stamp upgradeRequestId, stamp updatedAtUtcTicks.
 *   7. Commit and return AuthoritativeVillageUpgradeResult.FromUpgrade(Success).
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

        logger.info("executeUpgrade invoked", {
            uid: callerUid,
            upgradeRequestId: payload.upgradeRequestId,
            useBuildingIndex: payload.useBuildingIndex,
            buildingId: payload.buildingId,
            buildingIndex: payload.buildingIndex,
        });

        // eslint-disable-next-line @typescript-eslint/no-unused-vars
        const db = getFirestore();

        try {
            // TODO: translate from C# engine — see implementation checklist above.
            throw new HttpsError(
                "unimplemented",
                "Translate engine logic from AuthoritativeVillageUpgradeEngine.cs"
            );
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
