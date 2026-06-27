import { onCall, HttpsError, CallableRequest } from "firebase-functions/v2/https";
import { logger } from "firebase-functions/v2";
import { getFirestore } from "firebase-admin/firestore";

import {
    AuthoritativeStealRequest,
    AuthoritativeStealStatus,
    VoodooStabRequest,
    VoodooStabResult,
    VoodooStabStatus,
} from "./types";
import { runStealTransaction } from "./internal/stealEngine";
import { loadSnapshot, nowUtcTicks } from "./internal/playerSnapshotHelpers";

/**
 * executeVoodooStab — executes one stab in an active voodoo session.
 *
 * Each call: reads /stealSessions/{sessionId}, validates the caller is the
 * session's thief, rolls a random steal amount, then runs the shared steal
 * engine against thief + victim. All four docs (session, thief, victim,
 * impact stamp) commit atomically.
 *
 * The steal amount is rolled HERE — the client never gets to choose it.
 * Tuning constants live as `STEAL_*` below; this is the SINGLE source of
 * truth (no ScriptableObject equivalent on the server).
 */

/** Minimum fraction of victim coins stolen per stab. */
const STEAL_PERCENT_MIN = 0.005;

/** Maximum fraction of victim coins stolen per stab. */
const STEAL_PERCENT_MAX = 0.02;

/** Absolute ceiling — protects whales from getting drained too fast. */
const STEAL_ABSOLUTE_CEILING = 100000;

/** Firestore collection holding the /stealSessions/{sessionId} docs. */
const STEAL_SESSIONS_COLLECTION = "stealSessions";

/** Shape of the /stealSessions/{sessionId} document written by beginVoodooSession. */
interface VoodooSessionDoc {
    thiefId: string;
    victimId: string;
    victimDisplayName: string;
    stabsUsed: number;
    maxStabs: number;
    /**
     * Captured at draw-time by the LaunchStealEffect — the draw multiplier
     * the thief had active when the steal card resolved. Applied to every
     * stab in this session: victim loses the rolled amount once, the thief
     * receives that amount × thiefMultiplier. Older sessions written before
     * this field existed are treated as multiplier=1.
     */
    thiefMultiplier?: number;
    createdAtUtcMs: number;
    expiresAtUtcMs: number;
    status: string;
}

/** Mirrors the C# VoodooSession.BuildImpactIdForStab naming convention. */
function buildImpactIdForStab(sessionId: string, stabNumber: number): string {
    return `voodoo:${sessionId}:${stabNumber}`;
}

/**
 * Computes the clamped steal amount for one stab. Pure function so it is
 * trivial to test — the only randomness is the percent roll.
 */
function rollStealAmount(victimCoins: number): number {
    if (victimCoins <= 0) {
        return 0;
    }
    const percent =
        STEAL_PERCENT_MIN + Math.random() * (STEAL_PERCENT_MAX - STEAL_PERCENT_MIN);
    const rawAmount = percent * victimCoins;
    // Pure percentage of the victim's balance — no flat floor. A low roll on a
    // small balance can floor to 0; that stab simply yields nothing (it is still
    // consumed), which is acceptable by design. The ceiling only caps how fast a
    // whale can be drained.
    return Math.min(STEAL_ABSOLUTE_CEILING, Math.floor(rawAmount));
}

/** Result for non-success paths that still need to return a typed Result. */
function failure(status: VoodooStabStatus, message: string): VoodooStabResult {
    return {
        status,
        stolenAmount: 0,
        stabsRemaining: 0,
        isDollBroken: false,
        thiefSnapshot: null,
        message,
    };
}

export const executeVoodooStab = onCall<VoodooStabRequest, Promise<VoodooStabResult>>(
    { region: "us-central1" },
    async (request: CallableRequest<VoodooStabRequest>): Promise<VoodooStabResult> => {
        if (!request.auth) {
            throw new HttpsError("unauthenticated", "Sign-in required.");
        }
        const callerUid = request.auth.uid;

        const payload = request.data;
        if (!payload || typeof payload !== "object") {
            throw new HttpsError("invalid-argument", "Request body is missing.");
        }
        if (
            !payload.sessionId ||
            typeof payload.sessionId !== "string" ||
            payload.sessionId.trim().length === 0
        ) {
            throw new HttpsError("invalid-argument", "sessionId is required.");
        }

        const sessionId = payload.sessionId.trim();
        logger.info("executeVoodooStab invoked", { uid: callerUid, sessionId });

        const db = getFirestore();
        const sessionRef = db.collection(STEAL_SESSIONS_COLLECTION).doc(sessionId);

        try {
            const result = await db.runTransaction<VoodooStabResult>(async (tx) => {
                // -----------------------------------------------------------
                // 1) Read + validate the session document.
                // -----------------------------------------------------------
                const sessionSnap = await tx.get(sessionRef);
                if (!sessionSnap.exists) {
                    return failure(
                        VoodooStabStatus.SessionNotFound,
                        "Session not found."
                    );
                }
                const session = sessionSnap.data() as VoodooSessionDoc | undefined;
                if (!session) {
                    return failure(
                        VoodooStabStatus.SessionNotFound,
                        "Session not found."
                    );
                }

                if (session.thiefId !== callerUid) {
                    return failure(
                        VoodooStabStatus.Unauthorized,
                        "Caller is not the session thief."
                    );
                }

                const nowMs = Date.now();
                if (session.status !== "active" || nowMs > session.expiresAtUtcMs) {
                    return failure(
                        VoodooStabStatus.SessionExpired,
                        "Session is no longer active."
                    );
                }

                if (session.stabsUsed >= session.maxStabs) {
                    return failure(
                        VoodooStabStatus.SessionExhausted,
                        "All stabs already consumed."
                    );
                }

                // -----------------------------------------------------------
                // 2) Read the victim doc BEFORE we know the roll amount. We
                //    need its coin count to compute the percentage steal. The
                //    steal engine itself will re-read both docs (Firestore
                //    transactions de-dup reads on the same ref).
                // -----------------------------------------------------------
                const victimData = await loadSnapshot(tx, db, session.victimId);
                const stabNumber = session.stabsUsed + 1;
                const impactId = buildImpactIdForStab(sessionId, stabNumber);

                const rolledAmount = rollStealAmount(victimData.snapshot.coins);

                // requestedAmount is the raw percentage roll (no flat floor).
                // The engine clamps it to the victim's balance and reports
                // VictimEmpty when nothing can be taken (a 0 roll, or an
                // already-empty victim) — that stab is still consumed.
                const stealRequest: AuthoritativeStealRequest = {
                    impactId,
                    requestedAmount: rolledAmount,
                    thiefPlayerId: callerUid,
                    victimPlayerId: session.victimId,
                    createdAtUtcTicks: nowUtcTicks(),
                };

                const thiefMultiplier =
                    typeof session.thiefMultiplier === "number" && session.thiefMultiplier >= 1
                        ? Math.floor(session.thiefMultiplier)
                        : 1;

                const stealResult = await runStealTransaction(
                    tx,
                    db,
                    stealRequest,
                    thiefMultiplier
                );

                // -----------------------------------------------------------
                // 3) Consume the stab regardless of victim-empty outcome (per
                //    spec: VictimEmpty still consumes the stab). Dedup hits
                //    are NOT expected here because impactId is freshly built
                //    from a strictly-increasing stab number — surface them as
                //    Error since they signal session corruption.
                // -----------------------------------------------------------
                let mappedStatus: VoodooStabStatus;
                switch (stealResult.status) {
                    case AuthoritativeStealStatus.Success:
                    case AuthoritativeStealStatus.AppliedPartially:
                        mappedStatus = VoodooStabStatus.Success;
                        break;
                    case AuthoritativeStealStatus.VictimEmpty:
                        mappedStatus = VoodooStabStatus.VictimEmpty;
                        break;
                    case AuthoritativeStealStatus.AlreadyApplied:
                        // Should not happen — impactId is unique per stab.
                        logger.warn("Voodoo stab impactId collision", {
                            sessionId,
                            impactId,
                        });
                        return failure(
                            VoodooStabStatus.Error,
                            "Duplicate stab impactId."
                        );
                    case AuthoritativeStealStatus.Unavailable:
                        return failure(
                            VoodooStabStatus.Error,
                            stealResult.message || "Player unavailable."
                        );
                    case AuthoritativeStealStatus.InvalidRequest:
                        return failure(
                            VoodooStabStatus.InvalidRequest,
                            stealResult.message || "Invalid steal request."
                        );
                    case AuthoritativeStealStatus.Error:
                    default:
                        return failure(
                            VoodooStabStatus.Error,
                            stealResult.message || "Steal failed."
                        );
                }

                const newStabsUsed = session.stabsUsed + 1;
                const isDollBroken = newStabsUsed >= session.maxStabs;
                const newStatus = isDollBroken ? "exhausted" : session.status;

                tx.set(sessionRef, {
                    ...session,
                    stabsUsed: newStabsUsed,
                    status: newStatus,
                });

                return {
                    status: mappedStatus,
                    stolenAmount: stealResult.stolenAmount,
                    stabsRemaining: session.maxStabs - newStabsUsed,
                    isDollBroken,
                    thiefSnapshot: stealResult.thiefSnapshot,
                    message: stealResult.message,
                };
            });

            return result;
        } catch (err) {
            // loadSnapshot can throw "not-found" if the victim doc was deleted
            // mid-session. Surface as Error so the client can clean up.
            if (err instanceof HttpsError && err.code === "not-found") {
                return failure(VoodooStabStatus.Error, "Player not found.");
            }
            if (err instanceof HttpsError) {
                throw err;
            }
            logger.error("executeVoodooStab failed", {
                uid: callerUid,
                sessionId,
                err,
            });
            return failure(VoodooStabStatus.Error, "Internal server error.");
        }
    }
);
