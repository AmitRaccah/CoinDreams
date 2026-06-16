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

export const beginVoodooSession = onCall<unknown, Promise<VoodooSessionBeginResult>>(
    { region: "us-central1" },
    async (request: CallableRequest<unknown>): Promise<VoodooSessionBeginResult> => {
        if (!request.auth) {
            throw new HttpsError("unauthenticated", "Sign-in required.");
        }
        const callerUid = request.auth.uid;

        logger.info("beginVoodooSession invoked", { uid: callerUid });

        const db = getFirestore();

        // TODO: MVP — `limit(50)` is NOT scalable past 50 players. Replace with
        // a random-key field strategy (e.g. each player doc stores a random
        // shard/bucket key and we range-query) or a Cloud Function-driven
        // sharding service before production.
        const playerDocs = await db
            .collection(PLAYERS_COLLECTION)
            .limit(VICTIM_SAMPLE_LIMIT)
            .get();

        const candidates = playerDocs.docs.filter((doc) => doc.id !== callerUid);
        if (candidates.length === 0) {
            throw new HttpsError("failed-precondition", "No victims available.");
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
