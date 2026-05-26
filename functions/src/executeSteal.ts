import { onCall, HttpsError, CallableRequest } from "firebase-functions/v2/https";
import { logger } from "firebase-functions/v2";
import { getFirestore } from "firebase-admin/firestore";

import {
    AuthoritativeStealRequest,
    AuthoritativeStealResult,
    AuthoritativeStealStatus,
} from "./types";

/**
 * executeSteal — server-authoritative coin steal between two players.
 *
 * Unlike draw/upgrade, this is a TWO-DOCUMENT transaction (thief + victim).
 * The caller is the THIEF; the victimPlayerId is included in the request body.
 *
 * TODO: Translate engine logic from
 *       `Assets/Scripts/Domain/Player/AuthoritativeStealEngine.cs`.
 *       Until that port lands, this callable returns HttpsError("unimplemented").
 *
 * Implementation checklist:
 *   1. Validate request shape (impactId, thief/victim ids, amount > 0).
 *   2. Reject if request.auth.uid !== thiefPlayerId.
 *   3. Reject if thief == victim.
 *   4. Open a Firestore transaction reading BOTH docs:
 *        players/{thiefPlayerId} and players/{victimPlayerId}.
 *      (Reads must precede writes inside the transaction.)
 *   5. Reject duplicate impactId on EITHER side via processedImpactIds.
 *   6. Compute actualAmount = min(requestedAmount, victim.coins).
 *      - If victim.coins == 0 -> VictimEmpty.
 *      - If actualAmount < requestedAmount -> AppliedPartially.
 *   7. victim.coins -= actualAmount; thief.coins += actualAmount.
 *   8. Stamp impactId into BOTH processedImpactIds arrays, bump revisions,
 *      stamp updatedAtUtcTicks on both.
 *   9. Commit and return AuthoritativeStealResult.Success / .Partial / .VictimEmpty.
 */
export const executeSteal = onCall<AuthoritativeStealRequest, Promise<AuthoritativeStealResult>>(
    { region: "us-central1" },
    async (
        request: CallableRequest<AuthoritativeStealRequest>
    ): Promise<AuthoritativeStealResult> => {
        if (!request.auth) {
            throw new HttpsError("unauthenticated", "Sign-in required.");
        }
        const callerUid = request.auth.uid;

        const payload = request.data;
        if (!payload || typeof payload !== "object") {
            throw new HttpsError("invalid-argument", "Request body is missing.");
        }
        if (!payload.impactId || typeof payload.impactId !== "string" || payload.impactId.trim().length === 0) {
            throw new HttpsError("invalid-argument", "impactId is required.");
        }
        if (!payload.thiefPlayerId || !payload.victimPlayerId) {
            throw new HttpsError("invalid-argument", "thiefPlayerId and victimPlayerId are required.");
        }
        if (typeof payload.requestedAmount !== "number" || payload.requestedAmount <= 0) {
            throw new HttpsError("invalid-argument", "requestedAmount must be > 0.");
        }
        if (payload.thiefPlayerId === payload.victimPlayerId) {
            throw new HttpsError("invalid-argument", "Thief and victim must differ.");
        }
        if (callerUid !== payload.thiefPlayerId) {
            // The caller must be the thief; you cannot trigger a steal on behalf of another user.
            throw new HttpsError("permission-denied", "Caller is not the thief.");
        }

        logger.info("executeSteal invoked", {
            uid: callerUid,
            impactId: payload.impactId,
            thief: payload.thiefPlayerId,
            victim: payload.victimPlayerId,
            requestedAmount: payload.requestedAmount,
        });

        // eslint-disable-next-line @typescript-eslint/no-unused-vars
        const db = getFirestore();

        try {
            // TODO: translate from C# engine — see implementation checklist above.
            throw new HttpsError(
                "unimplemented",
                "Translate engine logic from AuthoritativeStealEngine.cs"
            );
        } catch (err) {
            if (err instanceof HttpsError) {
                throw err;
            }
            logger.error("executeSteal failed", {
                uid: callerUid,
                impactId: payload.impactId,
                err,
            });
            const result: AuthoritativeStealResult = {
                status: AuthoritativeStealStatus.Error,
                thiefSnapshot: null,
                victimSnapshot: null,
                stolenAmount: 0,
                message: "Internal server error.",
            };
            return result;
        }
    }
);
