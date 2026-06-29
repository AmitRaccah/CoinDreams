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

/**
 * Minimum balance a player must hold to be surfaced as a steal target. Each
 * stab takes only a percentage of the victim's coins (no flat floor), so a
 * near-empty victim would hand the thief almost nothing across all three
 * stabs. This keeps every surfaced victim worth the session.
 */
const MIN_VICTIM_COINS = 100;

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
 *   2. Must hold at least MIN_VICTIM_COINS. Each stab takes only a percentage
 *      of the victim's balance (no flat floor), so a near-empty victim would
 *      yield close to nothing across all three stabs. The threshold keeps
 *      every surfaced victim worth the session. (A victim who also holds a
 *      shield stays eligible: the stab deflects but still advances the game.)
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
    return coins >= MIN_VICTIM_COINS;
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

        // Primary victim pool: players who clear MIN_VICTIM_COINS. Pushing the
        // coins filter into the query (instead of sampling VICTIM_SAMPLE_LIMIT
        // arbitrary docs and filtering in JS) is what keeps this working once
        // the DB fills with fresh playtest accounts that start at 0 coins —
        // those would otherwise crowd an unfiltered limit() window and starve
        // the result even though funded victims exist further down.
        //
        // TODO: MVP — limit() still isn't a uniform random sample past
        // VICTIM_SAMPLE_LIMIT eligible players. Replace with a random-key/shard
        // strategy before scaling. For now (dozens of players) it's fine.
        const eligibleDocs = await db
            .collection(PLAYERS_COLLECTION)
            .where("coins", ">=", MIN_VICTIM_COINS)
            .limit(VICTIM_SAMPLE_LIMIT)
            .get();

        let candidates = eligibleDocs.docs.filter((doc) =>
            isEligibleVictim(doc.id, doc.data() as Record<string, unknown>, callerUid),
        );

        // Fallback — a drawn Steal card must NEVER dead-end with "no victim".
        // If nobody clears the threshold (a young DB, or every funded account
        // got drained), surface the richest OTHER player regardless of the
        // floor. A near-empty victim yields little per stab, but the session
        // still opens, which is the contract the client relies on.
        if (candidates.length === 0) {
            const richestDocs = await db
                .collection(PLAYERS_COLLECTION)
                .orderBy("coins", "desc")
                .limit(VICTIM_SAMPLE_LIMIT)
                .get();
            candidates = richestDocs.docs.filter((doc) => doc.id !== callerUid);
        }

        if (candidates.length === 0) {
            logger.warn("beginVoodooSession found no other player to target", {
                uid: callerUid,
            });
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
