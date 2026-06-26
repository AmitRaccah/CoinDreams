import { randomUUID } from "crypto";

import { onCall, HttpsError, CallableRequest } from "firebase-functions/v2/https";
import { logger } from "firebase-functions/v2";
import { getFirestore } from "firebase-admin/firestore";

import { VoodooSessionBeginResult } from "./types";
import { PLAYERS_COLLECTION } from "./internal/playerSnapshotHelpers";

/**
 * beginVoodooSession — opens a new voodoo-doll steal session.
 *
 * The session lives at /stealSessions/{sessionId}. The thief (caller) picks
 * a random victim server-side; the resulting session has a fixed maxStabs
 * budget and TTL, both of which executeVoodooStab will validate on every
 * stab call.
 *
 * No request body: the server is the sole arbiter of victim choice. Callers
 * are NOT trusted to nominate their own targets.
 */

/** Hard cap on the number of stabs allowed per voodoo session. */
const MAX_STABS = 3;

/** Session TTL in milliseconds — sessions older than this are rejected. */
const SESSION_TTL_MS = 30 * 60 * 1000;

/** Firestore collection that holds /stealSessions/{sessionId} docs. */
const STEAL_SESSIONS_COLLECTION = "stealSessions";

/**
 * Upper bound on the random-victim sampling window. Anything past this is
 * not visited — see the TODO inside the handler for the scaling plan.
 */
const VICTIM_SAMPLE_LIMIT = 50;

/** Whitelist for the draw-multiplier (mirrors AuthoritativeDrawRequest.AllowedMultipliers in C#). */
const ALLOWED_THIEF_MULTIPLIERS: readonly number[] = [1, 2, 4, 8];

/** Sanitizer — anything not in the allowed set collapses to 1. */
function sanitizeMultiplier(raw: unknown): number {
    if (typeof raw !== "number" || !Number.isFinite(raw)) {
        return 1;
    }
    return ALLOWED_THIEF_MULTIPLIERS.includes(raw) ? raw : 1;
}

/**
 * Eligibility rules for showing a player as a steal target. Centralised so
 * new constraints (banned users, recent-victim cooldown, regional
 * matchmaking) plug in by adding a single line here instead of fattening
 * the handler.
 *
 * Today's rules:
 *   1. Cannot steal from self.
 *   2. Cannot steal from a player who has NEITHER coins NOR shields. A stab
 *      against such a victim is a pure no-op: stealEngine reports
 *      VictimEmpty on every call, leaving the thief with nothing to take
 *      and nothing to chip away at. Players with shields BUT no coins are
 *      still valid — the stab decrements their shield (a meaningful
 *      outcome), even though no coins move.
 */
function isEligibleVictim(
    docId: string,
    docData: Record<string, unknown>,
    callerUid: string,
): boolean {
    if (docId === callerUid) {
        return false;
    }
    const rawCoins = docData["coins"];
    const coins = typeof rawCoins === "number" && Number.isFinite(rawCoins) ? rawCoins : 0;
    const rawShields = docData["shields"];
    const shields = typeof rawShields === "number" && Number.isFinite(rawShields) ? rawShields : 0;
    return coins > 0 || shields > 0;
}

export const beginVoodooSession = onCall<unknown, Promise<VoodooSessionBeginResult>>(
    { region: "us-central1" },
    async (request: CallableRequest<unknown>): Promise<VoodooSessionBeginResult> => {
        if (!request.auth) {
            throw new HttpsError("unauthenticated", "Sign-in required.");
        }
        const callerUid = request.auth.uid;

        // The thief's draw multiplier flows in here from the client. It is
        // captured at draw-time (LaunchStealEffect) and persisted on the
        // session so every stab applies the SAME multiplier — even if the
        // client UI cycles to a different value mid-session.
        const payload = request.data as { multiplier?: unknown } | undefined;
        const thiefMultiplier = sanitizeMultiplier(payload?.multiplier);

        logger.info("beginVoodooSession invoked", { uid: callerUid, thiefMultiplier });

        const db = getFirestore();

        // TODO: MVP — `limit(50)` is NOT scalable past 50 players. Replace with
        // a random-key field strategy (e.g. each player doc stores a random
        // shard/bucket key and we range-query) or a Cloud Function-driven
        // sharding service before production.
        const playerDocs = await db
            .collection(PLAYERS_COLLECTION)
            .limit(VICTIM_SAMPLE_LIMIT)
            .get();

        const candidates = playerDocs.docs.filter((doc) =>
            isEligibleVictim(doc.id, doc.data() as Record<string, unknown>, callerUid),
        );
        if (candidates.length === 0) {
            throw new HttpsError("failed-precondition", "No eligible victims available.");
        }

        const pick = candidates[Math.floor(Math.random() * candidates.length)];
        const victimId = pick.id;

        // displayName is not part of the PlayerProfileSnapshot contract, but
        // some doc variants may include it. We use a safe `unknown` lookup so
        // we never trust the field's type — if it's missing or malformed we
        // fall back to a synthesized name.
        const pickData = pick.data() as Record<string, unknown>;
        const rawDisplayName = pickData["displayName"];
        const victimDisplayName =
            typeof rawDisplayName === "string" && rawDisplayName.trim().length > 0
                ? rawDisplayName.trim()
                : `Player_${victimId.substring(0, 6)}`;

        const sessionId = randomUUID();
        const createdAtUtcMs = Date.now();
        const expiresAtUtcMs = createdAtUtcMs + SESSION_TTL_MS;

        await db
            .collection(STEAL_SESSIONS_COLLECTION)
            .doc(sessionId)
            .set({
                thiefId: callerUid,
                victimId,
                victimDisplayName,
                stabsUsed: 0,
                maxStabs: MAX_STABS,
                thiefMultiplier,
                createdAtUtcMs,
                expiresAtUtcMs,
                status: "active",
            });

        return {
            sessionId,
            victimPlayerId: victimId,
            victimDisplayName,
            maxStabs: MAX_STABS,
        };
    }
);
