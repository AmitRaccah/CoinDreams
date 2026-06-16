import { firestore } from "firebase-admin";
import { HttpsError } from "firebase-functions/v2/https";

import { PlayerProfileSnapshot } from "../types";

/**
 * Shared low-level helpers for working with /players/{playerId} documents.
 *
 * These are the TypeScript siblings of the C# PlayerProfile mutation helpers:
 *   - Dedup of processedImpactIds          ← PlayerProfile.RecordProcessedImpactId
 *   - Time-based energy regen              ← EnergyService.ApplyRegenInternal + EnergyRegenCalculator
 *   - Revision + updatedAtUtcTicks stamping ← PlayerProfile.MarkChanged
 *
 * Kept intentionally framework-light so the same module can back executeDraw,
 * executeUpgrade and executeSteal/Voodoo without leaking transaction state.
 */

export const PLAYERS_COLLECTION = "players";

/** Mirrors PlayerProfile.MaxProcessedImpactIds (C#). */
export const MAX_PROCESSED_IMPACT_IDS = 10000;

/** Number of .NET ticks per millisecond — used for tick<->ms conversion. */
export const TICKS_PER_MILLISECOND = 10000;

/** Number of .NET ticks per second — used for energy regen math. */
export const TICKS_PER_SECOND = 10000000;

/**
 * .NET DateTime.MinValue (1970-01-01 UTC) expressed in ticks. We use this to
 * convert JavaScript Date.now() (Unix ms) into the .NET tick space that the
 * C# client stores in `lastRegenUtcTicks`.
 */
const TICKS_AT_UNIX_EPOCH = 621355968000000000;

/**
 * Returns "now" in .NET tick units (100ns ticks since DateTime.MinValue UTC).
 * Matches the values produced by C# ITimeProvider.GetUtcNowTicks() on the client.
 */
export function nowUtcTicks(): number {
    return TICKS_AT_UNIX_EPOCH + Date.now() * TICKS_PER_MILLISECOND;
}

/**
 * Loads a /players/{playerId} document inside a transaction. Throws an
 * HttpsError("not-found", ...) if the doc is missing — callers are expected to
 * catch and translate this into a domain-level Unavailable result.
 */
export async function loadSnapshot(
    tx: firestore.Transaction,
    db: firestore.Firestore,
    playerId: string
): Promise<{ ref: firestore.DocumentReference; snapshot: PlayerProfileSnapshot }> {
    if (!playerId || typeof playerId !== "string" || playerId.trim().length === 0) {
        throw new HttpsError("invalid-argument", "playerId is required.");
    }

    const ref = db.collection(PLAYERS_COLLECTION).doc(playerId);
    const docSnap = await tx.get(ref);
    if (!docSnap.exists) {
        throw new HttpsError("not-found", `Player document missing: ${playerId}`);
    }

    // The C# JsonUtility serializer guarantees the field layout. We trust the
    // shape here because all writes to /players go through Cloud Functions.
    const data = docSnap.data() as PlayerProfileSnapshot | undefined;
    if (!data) {
        throw new HttpsError("not-found", `Player document empty: ${playerId}`);
    }

    // Defensive normalization — older docs may pre-date a field; keep parity
    // with the C# PlayerProfileSnapshot constructor defaults.
    const snapshot: PlayerProfileSnapshot = {
        playerId: data.playerId ?? playerId,
        revision: data.revision ?? 0,
        coins: data.coins ?? 0,
        currentEnergy: data.currentEnergy ?? 0,
        regenMaxEnergy: data.regenMaxEnergy ?? 0,
        regenIntervalSeconds: data.regenIntervalSeconds ?? 0,
        lastRegenUtcTicks: data.lastRegenUtcTicks ?? 0,
        villageLevels: Array.isArray(data.villageLevels) ? [...data.villageLevels] : [],
        processedImpactIds: Array.isArray(data.processedImpactIds) ? [...data.processedImpactIds] : [],
        updatedAtUtcTicks: data.updatedAtUtcTicks ?? 0,
        schemaVersion: data.schemaVersion ?? 0,
    };

    return { ref, snapshot };
}

/** Returns true if the snapshot has already processed this impactId (trim-insensitive). */
export function hasProcessedImpact(snapshot: PlayerProfileSnapshot, impactId: string): boolean {
    if (!impactId) {
        return false;
    }
    const target = impactId.trim();
    if (target.length === 0) {
        return false;
    }
    return snapshot.processedImpactIds.includes(target);
}

/**
 * Appends an impactId to the snapshot's processedImpactIds, evicting the
 * oldest entry once the cap is exceeded. Mirrors the bounded queue behaviour
 * of C# PlayerProfile.RecordProcessedImpactId.
 */
export function stampProcessedImpact(snapshot: PlayerProfileSnapshot, impactId: string): void {
    if (!impactId) {
        return;
    }
    const target = impactId.trim();
    if (target.length === 0) {
        return;
    }
    if (snapshot.processedImpactIds.includes(target)) {
        return;
    }
    snapshot.processedImpactIds.push(target);
    while (snapshot.processedImpactIds.length > MAX_PROCESSED_IMPACT_IDS) {
        snapshot.processedImpactIds.shift();
    }
}

/**
 * Mutates `snapshot` in place by applying time-based energy regen up to
 * regenMaxEnergy. Mirrors the C# EnergyService.ApplyRegenInternal flow:
 *   - Only full intervals tick.
 *   - The anchor advances only by the number of intervals applied (so the
 *     fractional remainder carries over to the next call).
 *   - When current is already at or above max, the anchor is fast-forwarded
 *     to "skip" the missed intervals (no clock-skew freebies later).
 */
export function applyEnergyRegen(snapshot: PlayerProfileSnapshot, nowTicks: number): void {
    let interval = snapshot.regenIntervalSeconds;
    if (interval <= 0) {
        interval = 1;
    }
    const intervalTicks = interval * TICKS_PER_SECOND;
    const last = snapshot.lastRegenUtcTicks;

    if (nowTicks <= last) {
        return;
    }

    const elapsed = nowTicks - last;
    // Integer floor division — JavaScript numbers are 64-bit floats but our
    // tick range comfortably fits inside Number.MAX_SAFE_INTEGER (~year 30000).
    const gained = Math.floor(elapsed / intervalTicks);
    if (gained <= 0) {
        return;
    }

    // Already capped — fast-forward the anchor and return.
    if (snapshot.currentEnergy >= snapshot.regenMaxEnergy) {
        snapshot.lastRegenUtcTicks = last + gained * intervalTicks;
        return;
    }

    const missing = snapshot.regenMaxEnergy - snapshot.currentEnergy;
    const applied = gained > missing ? missing : gained;
    snapshot.currentEnergy += applied;
    if (snapshot.currentEnergy > snapshot.regenMaxEnergy) {
        snapshot.currentEnergy = snapshot.regenMaxEnergy;
    }

    snapshot.lastRegenUtcTicks = last + applied * intervalTicks;

    // If we hit the cap with this batch, fast-forward over the remaining
    // intervals so future ticks restart from "now".
    if (snapshot.currentEnergy >= snapshot.regenMaxEnergy && gained > applied) {
        snapshot.lastRegenUtcTicks += (gained - applied) * intervalTicks;
    }
}

/**
 * Bumps revision and stamps server-side timestamps. Mirrors the bookkeeping
 * done by C# PlayerProfile.MarkChanged immediately before a save.
 */
export function stampUpdate(snapshot: PlayerProfileSnapshot, nowTicks: number): void {
    snapshot.revision = (snapshot.revision ?? 0) + 1;
    snapshot.updatedAtUtcTicks = nowTicks;
}
