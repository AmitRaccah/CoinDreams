import { onCall, HttpsError, CallableRequest } from "firebase-functions/v2/https";
import { logger } from "firebase-functions/v2";
import { getFirestore } from "firebase-admin/firestore";

import {
    AuthoritativeStealRequest,
    AuthoritativeStealResult,
    AuthoritativeStealStatus,
} from "./types";
import { runStealTransaction } from "./internal/stealEngine";

/**
 * executeSteal — server-authoritative coin steal between two players.
 *
 * Unlike draw/upgrade, this is a TWO-DOCUMENT transaction (thief + victim).
 * The caller is the THIEF; the victimPlayerId is included in the request body.
 *
 * The engine logic lives in `internal/stealEngine.ts` so it can be reused by
 * executeVoodooStab. See AuthoritativeStealEngine.cs (lines 8-126) for the
 * C# parity reference.
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

        const db = getFirestore();

        try {
            const result = await db.runTransaction(async (tx) => {
                return runStealTransaction(tx, db, payload);
            });
            return result;
        } catch (err) {
            // not-found from loadSnapshot maps to a domain-level Unavailable
            // result so the Unity client can surface a readable message
            // without tearing down the callable layer.
            if (err instanceof HttpsError && err.code === "not-found") {
                return {
                    status: AuthoritativeStealStatus.Unavailable,
                    thiefSnapshot: null,
                    victimSnapshot: null,
                    stolenAmount: 0,
                    message: "Player not found",
                };
            }
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
