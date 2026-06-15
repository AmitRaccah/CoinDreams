import { onCall, HttpsError, CallableRequest } from "firebase-functions/v2/https";
import { logger } from "firebase-functions/v2";
import { getFirestore } from "firebase-admin/firestore";

import {
    AuthoritativeDrawRequest,
    AuthoritativeDrawResult,
    AuthoritativeDrawStatus,
} from "./types";

/**
 * executeDraw — server-authoritative card draw.
 *
 * Replaces the client-side AuthoritativeDrawEngine running inside Unity. The
 * client passes only the draw catalog and a deterministic drawId; the server
 * is the sole source of truth for energy, coins, weighted RNG and the
 * processedImpactIds idempotency stamp.
 *
 * TODO: Translate engine logic from `Assets/Scripts/Domain/Cards/AuthoritativeDrawEngine.cs`.
 *       Until that port lands, this callable returns HttpsError("unimplemented").
 *
 * Implementation checklist (in order):
 *   1. Validate request shape (drawId non-empty, multiplier in {1,2,4,8},
 *      cards non-empty, weights non-negative).
 *   2. Open a Firestore transaction on `players/{uid}`.
 *   3. Reject duplicate drawIds via `snapshot.processedImpactIds`.
 *   4. Regenerate energy from `lastRegenUtcTicks` -> now.
 *   5. Validate energy >= effectiveCost (cost * multiplier).
 *   6. Cryptographically-random weighted pick from `request.cards`.
 *   7. Apply effects (AddCoins / AddEnergy / LaunchSteal).
 *   8. Bump revision, stamp drawId into processedImpactIds (cap size),
 *      stamp updatedAtUtcTicks.
 *   9. Commit and return AuthoritativeDrawResult.Success.
 */
export const executeDraw = onCall<AuthoritativeDrawRequest, Promise<AuthoritativeDrawResult>>(
    { region: "us-central1" },
    async (request: CallableRequest<AuthoritativeDrawRequest>): Promise<AuthoritativeDrawResult> => {
        // 1. Authentication.
        if (!request.auth) {
            throw new HttpsError("unauthenticated", "Sign-in required.");
        }
        const callerUid = request.auth.uid;

        // 2. Basic request validation.
        const payload = request.data;
        if (!payload || typeof payload !== "object") {
            throw new HttpsError("invalid-argument", "Request body is missing.");
        }
        if (!payload.drawId || typeof payload.drawId !== "string" || payload.drawId.trim().length === 0) {
            throw new HttpsError("invalid-argument", "drawId is required.");
        }
        if (!Array.isArray(payload.cards) || payload.cards.length === 0) {
            throw new HttpsError("invalid-argument", "cards must be a non-empty array.");
        }

        logger.info("executeDraw invoked", {
            uid: callerUid,
            drawId: payload.drawId,
            cardCount: payload.cards.length,
            multiplier: payload.requestedMultiplier,
        });

        // 3. Acquire Firestore handle (used once the body is ported).
        // eslint-disable-next-line @typescript-eslint/no-unused-vars
        const db = getFirestore();

        try {
            // TODO: translate from C# engine — see implementation checklist above.
            throw new HttpsError(
                "unimplemented",
                "Translate engine logic from AuthoritativeDrawEngine.cs"
            );
        } catch (err) {
            if (err instanceof HttpsError) {
                throw err;
            }
            logger.error("executeDraw failed", { uid: callerUid, drawId: payload.drawId, err });
            // Return an Error result so the Unity client can render it without
            // tearing down. Throwing here would surface as HttpsError("internal").
            const result: AuthoritativeDrawResult = {
                status: AuthoritativeDrawStatus.Error,
                snapshot: null,
                drawnCardId: "",
                stealTriggerId: "",
                message: "Internal server error.",
            };
            return result;
        }
    }
);
