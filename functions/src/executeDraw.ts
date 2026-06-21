import { onCall, HttpsError, CallableRequest } from "firebase-functions/v2/https";
import { logger } from "firebase-functions/v2";
import { getFirestore } from "firebase-admin/firestore";

import {
    AuthoritativeDrawCardDefinition,
    AuthoritativeDrawEffectType,
    AuthoritativeDrawRequest,
    AuthoritativeDrawResult,
    AuthoritativeDrawStatus,
    PlayerProfileSnapshot,
} from "./types";
import {
    loadSnapshot,
    hasProcessedImpact,
    stampProcessedImpact,
    applyEnergyRegen,
    stampUpdate,
    nowUtcTicks,
    // PLAYERS_COLLECTION is intentionally not imported here — the helper owns
    // collection reads. We rely on loadSnapshot to return the document ref.
} from "./internal/playerSnapshotHelpers";

/**
 * executeDraw — server-authoritative card draw.
 *
 * Replaces the client-side AuthoritativeDrawEngine running inside Unity. The
 * client passes only the draw catalog and a deterministic drawId; the server
 * is the sole source of truth for energy, coins, weighted RNG and the
 * processedImpactIds idempotency stamp.
 *
 * Port of:
 *   - Assets/Scripts/Domain/Cards/AuthoritativeDrawEngine.cs
 *   - Assets/Scripts/Domain/Cards/WeightedRandomCardDeck.cs
 *   - Assets/Scripts/Domain/Cards/RewardContext.cs (ScaleAmount)
 *   - Assets/Scripts/Infrastructure/Persistence/FirestorePlayerRepository.cs
 *     (StableSeedFromDrawId — FNV-1a 32-bit)
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
        if (typeof payload.drawCost !== "number" || payload.drawCost < 0) {
            throw new HttpsError("invalid-argument", "drawCost must be zero or positive.");
        }

        logger.info("executeDraw invoked", {
            uid: callerUid,
            drawId: payload.drawId,
            cardCount: payload.cards.length,
            multiplier: payload.requestedMultiplier,
        });

        const db = getFirestore();

        try {
            const result = await db.runTransaction(async (tx): Promise<AuthoritativeDrawResult> => {
                // 1. Load player snapshot via the shared helper (read before write).
                const playerData = await loadSnapshot(tx, db, callerUid);
                const snapshot = playerData.snapshot;

                // 2. Idempotency — bail out if this drawId already landed.
                if (hasProcessedImpact(snapshot, payload.drawId)) {
                    return alreadyProcessedResult(snapshot);
                }

                // 3. Validate multiplier — mirrors AuthoritativeDrawRequest ctor
                //    in AuthoritativeDrawContracts.cs (fall back to 1 if not in
                //    the whitelist).
                const multiplier = ALLOWED_MULTIPLIERS.includes(payload.requestedMultiplier)
                    ? payload.requestedMultiplier
                    : 1;

                // 4. Apply energy regen against the current server clock, then
                //    check cost.
                const now = nowUtcTicks();
                applyEnergyRegen(snapshot, now);
                const effectiveCost = scaleDrawCost(payload.drawCost, multiplier);

                // 5. Filter the deck — matches BuildRuntimeCards in
                //    AuthoritativeDrawEngine.cs lines 155-196.
                const cards = filterRuntimeCards(payload.cards);
                if (cards.length === 0) {
                    return deckEmptyResult(snapshot);
                }

                if (snapshot.currentEnergy < effectiveCost) {
                    return notEnoughEnergyResult(snapshot);
                }

                // 6. Deterministic weighted pick — seeded from drawId so retries
                //    of the same drawId always pick the same card (this is the
                //    correctness guarantee that lets the dedup check above be
                //    idempotent).
                const seed = fnv1a32(payload.drawId);
                const rng = new MulberryRng(seed);
                const chosen = pickWeightedCard(cards, rng);

                // 7. Spend the energy cost.
                snapshot.currentEnergy -= effectiveCost;

                // 8. Apply effects. Multiplier scales AddCoins/AddEnergy amounts
                //    via ScaleAmount (RewardContext.cs lines 72-86). LaunchSteal
                //    only sets stealTriggerId on the result — no snapshot
                //    mutation, the actual steal runs in a separate callable.
                let stealTriggerId = "";
                const effects = chosen.effects ?? [];
                for (const effect of effects) {
                    if (!effect) {
                        continue;
                    }
                    switch (effect.effectType) {
                        case AuthoritativeDrawEffectType.AddCoins: {
                            const delta = scaleAmount(effect.intValue, multiplier);
                            snapshot.coins = clampToInt32(snapshot.coins + delta);
                            break;
                        }
                        case AuthoritativeDrawEffectType.AddEnergy: {
                            const delta = scaleAmount(effect.intValue, multiplier);
                            const next = snapshot.currentEnergy + delta;
                            // Cap at regenMaxEnergy, matching the energy service
                            // contract that AddResourceEffect routes through.
                            snapshot.currentEnergy = Math.min(snapshot.regenMaxEnergy, next);
                            break;
                        }
                        case AuthoritativeDrawEffectType.LaunchSteal: {
                            stealTriggerId = effect.stringValue ?? "";
                            break;
                        }
                        case AuthoritativeDrawEffectType.AddShields: {
                            // Shield rule: drawing at multiplier M wants to
                            // add M shields. Fill up to the cap, refund the
                            // overflow as energy (also capped). Without this
                            // refund the player loses value at high multipliers
                            // once the cap is reached — the user-facing rule is
                            // "you never lose a draw outcome to the cap".
                            const requested = scaleAmount(effect.intValue, multiplier);
                            const space = snapshot.maxShields - snapshot.shields;
                            const filled = space > 0 ? Math.min(requested, space) : 0;
                            const overflow = requested - filled;
                            if (filled > 0) {
                                snapshot.shields = clampToInt32(snapshot.shields + filled);
                            }
                            if (overflow > 0) {
                                const next = snapshot.currentEnergy + overflow;
                                snapshot.currentEnergy = Math.min(snapshot.regenMaxEnergy, next);
                            }
                            break;
                        }
                        default: {
                            // Unknown effect types are silently ignored to
                            // match AuthoritativeEffectRegistry.TryCreate
                            // behaviour (returns false, skips).
                            break;
                        }
                    }
                }

                // 9. Stamp impact + bump revision + updated timestamp.
                stampProcessedImpact(snapshot, payload.drawId);
                stampUpdate(snapshot, now);

                // 10. Commit.
                tx.set(playerData.ref, snapshot);

                return successResult(snapshot, chosen.cardId, stealTriggerId);
            });
            return result;
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

// ---------------------------------------------------------------------------
// Multiplier whitelist — mirrors AuthoritativeDrawRequest.AllowedMultipliers.
// ---------------------------------------------------------------------------

const ALLOWED_MULTIPLIERS: readonly number[] = [1, 2, 4, 8];

// ---------------------------------------------------------------------------
// Helpers — local to executeDraw.ts because they encode draw-specific logic
// (deterministic RNG, cost scaling, deck filtering, result shapes).
// ---------------------------------------------------------------------------

const INT32_MAX = 2147483647;
const INT32_MIN = -2147483648;

/**
 * FNV-1a 32-bit hash over the UTF-8 bytes of `s`.
 *
 * Port of FirestorePlayerRepository.StableSeedFromDrawId (lines 579-599). The
 * C# version uses `unchecked` int32 arithmetic with the standard FNV
 * constants. We replicate that by working in 32-bit unsigned space (`>>> 0`
 * after every multiply) so the same drawId hashes to the same seed across
 * the client and server, which is what makes retries deterministic.
 */
function fnv1a32(s: string): number {
    if (!s || s.length === 0) {
        return 0;
    }

    const FNV_OFFSET_BASIS = 0x811c9dc5; // 2166136261
    const FNV_PRIME = 0x01000193;        // 16777619

    const bytes = Buffer.from(s, "utf-8");
    let hash = FNV_OFFSET_BASIS;
    for (let i = 0; i < bytes.length; i++) {
        hash ^= bytes[i];
        // Math.imul keeps the multiply in 32-bit signed space; >>> 0 then
        // reinterprets it as unsigned for the next iteration.
        hash = Math.imul(hash, FNV_PRIME) >>> 0;
    }
    // Reinterpret as signed int32 to match the C# return type — callers that
    // feed this into Mulberry32 still treat it as a 32-bit bit pattern.
    return hash | 0;
}

/**
 * Mulberry32 — a tiny, fast, well-distributed 32-bit PRNG. Chosen for
 * determinism: given the same seed, `nextFloat()` always produces the same
 * sequence in [0, 1), which is what we need for retry idempotency.
 *
 * Reference: https://github.com/bryc/code/blob/master/jshash/PRNGs.md#mulberry32
 */
class MulberryRng {
    private state: number;

    constructor(seed: number) {
        // Force into 32-bit unsigned space so a zero seed still produces a
        // valid sequence (Mulberry32 is robust at zero, but normalising keeps
        // the math predictable).
        this.state = seed >>> 0;
    }

    /** Returns a float in [0, 1) — uniform to 32-bit precision. */
    nextFloat(): number {
        this.state = (this.state + 0x6d2b79f5) >>> 0;
        let t = this.state;
        t = Math.imul(t ^ (t >>> 15), t | 1);
        t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
        return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
    }
}

/**
 * Scale a reward amount by the draw multiplier, clamped to int32 range.
 *
 * Port of RewardContext.ScaleAmount (lines 72-86). The C# version uses long
 * arithmetic for overflow detection and clamps to int.MaxValue / int.MinValue.
 * JS numbers are 64-bit floats so the multiply itself never overflows for
 * realistic inputs, but we still clamp so downstream consumers (and the C#
 * client when it deserializes the snapshot) see int32-safe values.
 */
function scaleAmount(amount: number, multiplier: number): number {
    if (amount === 0 || multiplier === 0) {
        return 0;
    }
    const scaled = amount * multiplier;
    return clampToInt32(scaled);
}

/**
 * Scale the draw cost — matches AuthoritativeDrawEngine.ScaleDrawCost (lines
 * 228-242). Negative costs are coerced to zero (the C# version returns 0 for
 * any non-positive base cost).
 */
function scaleDrawCost(drawCost: number, multiplier: number): number {
    if (drawCost <= 0) {
        return 0;
    }
    const scaled = drawCost * multiplier;
    if (scaled > INT32_MAX) {
        return INT32_MAX;
    }
    return Math.trunc(scaled);
}

function clampToInt32(value: number): number {
    if (value > INT32_MAX) {
        return INT32_MAX;
    }
    if (value < INT32_MIN) {
        return INT32_MIN;
    }
    return Math.trunc(value);
}

/**
 * Filter the request's card array to entries that have a non-empty id and a
 * positive weight. Mirrors AuthoritativeDrawEngine.BuildRuntimeCards (lines
 * 155-196) — including the cardId.Trim() pass.
 */
function filterRuntimeCards(sourceCards: AuthoritativeDrawCardDefinition[]): AuthoritativeDrawCardDefinition[] {
    const result: AuthoritativeDrawCardDefinition[] = [];
    for (const card of sourceCards) {
        if (!card) {
            continue;
        }
        if (typeof card.cardId !== "string") {
            continue;
        }
        const trimmedId = card.cardId.trim();
        if (trimmedId.length === 0) {
            continue;
        }
        if (typeof card.weight !== "number" || card.weight <= 0) {
            continue;
        }
        // Preserve the original shape but use the trimmed id so the chosen
        // card's id round-trips cleanly back to the client.
        result.push({
            cardId: trimmedId,
            weight: Math.trunc(card.weight),
            effects: Array.isArray(card.effects) ? card.effects : [],
        });
    }
    return result;
}

/**
 * Weighted card pick. Mirrors the WeightedRandomCardDeck contract: walk the
 * cards in order, subtract the weight, and return the card whose weight
 * "covers" the roll. The roll is drawn from the deterministic RNG so retries
 * of the same drawId yield the same card.
 *
 * Precondition: `cards` is non-empty and every weight is > 0 (enforced by
 * filterRuntimeCards above).
 */
function pickWeightedCard(
    cards: AuthoritativeDrawCardDefinition[],
    rng: MulberryRng
): AuthoritativeDrawCardDefinition {
    let totalWeight = 0;
    for (const c of cards) {
        totalWeight += c.weight;
    }

    if (totalWeight <= 0) {
        // Defensive — filterRuntimeCards rejects zero/negative weights, so
        // this branch is only reachable if the inputs were already filtered
        // and every weight was equal. Fall back to the first card.
        return cards[0];
    }

    let roll = Math.floor(rng.nextFloat() * totalWeight);
    if (roll >= totalWeight) {
        roll = totalWeight - 1;
    }

    for (const c of cards) {
        if (roll < c.weight) {
            return c;
        }
        roll -= c.weight;
    }

    // Unreachable given the loop invariants, but keep a safe fallback so the
    // type system is satisfied and we never throw on a numerically odd input.
    return cards[cards.length - 1];
}

// ---------------------------------------------------------------------------
// Result builders — keep field shapes identical to the skeleton above.
// ---------------------------------------------------------------------------

function successResult(
    snapshot: PlayerProfileSnapshot,
    drawnCardId: string,
    stealTriggerId: string
): AuthoritativeDrawResult {
    return {
        status: AuthoritativeDrawStatus.Success,
        snapshot,
        drawnCardId: drawnCardId ?? "",
        stealTriggerId: stealTriggerId ?? "",
        message: "",
    };
}

function notEnoughEnergyResult(snapshot: PlayerProfileSnapshot): AuthoritativeDrawResult {
    return {
        status: AuthoritativeDrawStatus.NotEnoughEnergy,
        snapshot,
        drawnCardId: "",
        stealTriggerId: "",
        message: "Not enough energy.",
    };
}

function deckEmptyResult(snapshot: PlayerProfileSnapshot): AuthoritativeDrawResult {
    return {
        status: AuthoritativeDrawStatus.DeckEmpty,
        snapshot,
        drawnCardId: "",
        stealTriggerId: "",
        message: "Deck is empty.",
    };
}

function alreadyProcessedResult(snapshot: PlayerProfileSnapshot): AuthoritativeDrawResult {
    return {
        status: AuthoritativeDrawStatus.AlreadyProcessed,
        snapshot,
        drawnCardId: "",
        stealTriggerId: "",
        message: "Draw already processed.",
    };
}
