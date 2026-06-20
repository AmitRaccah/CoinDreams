import { firestore } from "firebase-admin";

import {
    AuthoritativeStealRequest,
    AuthoritativeStealResult,
    AuthoritativeStealStatus,
} from "../types";
import {
    applyEnergyRegen,
    hasProcessedImpact,
    loadSnapshot,
    nowUtcTicks,
    stampProcessedImpact,
    stampUpdate,
    SHIELD_DEFLECTION_RATIO,
} from "./playerSnapshotHelpers";

/**
 * Core steal engine — the single source of truth that backs both
 * executeSteal (direct steal triggered by a draw) and executeVoodooStab
 * (multi-stab voodoo session).
 *
 * Port of AuthoritativeStealEngine.TryExecute (C#, lines 8-126). The function
 * is designed to be called from INSIDE an existing Firestore transaction so
 * voodoo can also write the /stealSessions doc atomically with the player
 * docs. All reads happen before writes per Firestore transaction semantics.
 *
 * On a missing player document, a not-found HttpsError bubbles out of
 * loadSnapshot — the wrapping callable should translate that into an
 * Unavailable Result.
 */
export async function runStealTransaction(
    tx: firestore.Transaction,
    db: firestore.Firestore,
    request: AuthoritativeStealRequest,
    thiefBonusMultiplier: number = 1
): Promise<AuthoritativeStealResult> {
    // Asymmetric application: the victim loses the rolled `stolen` amount, the
    // thief receives `stolen * bonusMultiplier`. The "extra" coins are minted
    // by design — this models the "x2/x4/x8 draw multiplier rewards the thief
    // without punishing the victim further" rule. Floor and clamp to >= 1 so
    // a malformed multiplier never zeroes the thief's gain or burns coins.
    const safeBonusMultiplier =
        Number.isFinite(thiefBonusMultiplier) && thiefBonusMultiplier >= 1
            ? Math.floor(thiefBonusMultiplier)
            : 1;
    const thiefData = await loadSnapshot(tx, db, request.thiefPlayerId);
    const victimData = await loadSnapshot(tx, db, request.victimPlayerId);

    const now = nowUtcTicks();

    // Dedup mirrors C# PlayerProfile.processedImpactSet — either side having
    // seen this impactId means the steal already landed and we must NOT
    // double-apply.
    if (
        hasProcessedImpact(thiefData.snapshot, request.impactId) ||
        hasProcessedImpact(victimData.snapshot, request.impactId)
    ) {
        // Still apply regen so the snapshots returned to the client are
        // up-to-date. C# does the same in the AlreadyApplied branch.
        applyEnergyRegen(thiefData.snapshot, now);
        applyEnergyRegen(victimData.snapshot, now);
        return {
            status: AuthoritativeStealStatus.AlreadyApplied,
            thiefSnapshot: thiefData.snapshot,
            victimSnapshot: victimData.snapshot,
            stolenAmount: 0,
            message: "Impact already processed.",
        };
    }

    applyEnergyRegen(thiefData.snapshot, now);
    applyEnergyRegen(victimData.snapshot, now);

    const victimCoins = victimData.snapshot.coins;
    const requested = request.requestedAmount;
    const rawStolen = Math.min(requested, victimCoins);

    if (rawStolen <= 0) {
        // C# returns VictimEmpty without writing back — we follow the same
        // pattern and skip the writes. Regen-only state is recomputed on the
        // next interaction.
        return {
            status: AuthoritativeStealStatus.VictimEmpty,
            thiefSnapshot: thiefData.snapshot,
            victimSnapshot: victimData.snapshot,
            stolenAmount: 0,
            message: "Victim has no coins to steal.",
        };
    }

    // Shield-deflection rule: a shielded victim loses NO coins, the thief
    // still receives a fraction (SHIELD_DEFLECTION_RATIO) of what they would
    // have, and the victim's shield count decrements by one. The fraction is
    // applied to the post-multiplier gain so high-multiplier draws still feel
    // rewarding to the thief, just halved.
    let coinsLostByVictim: number;
    let thiefGain: number;
    // Coalesce once and reuse — referencing `victimData.snapshot.shields` on
    // line 102 would hit `undefined - 1 = NaN` for legacy docs that pre-date
    // the shield field, even though the gate on the previous line coalesced.
    const currentVictimShields = victimData.snapshot.shields ?? 0;
    if (currentVictimShields > 0) {
        coinsLostByVictim = 0;
        thiefGain = Math.floor(rawStolen * safeBonusMultiplier * SHIELD_DEFLECTION_RATIO);
        victimData.snapshot.shields = currentVictimShields - 1;
    } else {
        coinsLostByVictim = rawStolen;
        thiefGain = rawStolen * safeBonusMultiplier;
    }

    victimData.snapshot.coins -= coinsLostByVictim;
    thiefData.snapshot.coins += thiefGain;

    stampProcessedImpact(victimData.snapshot, request.impactId);
    stampProcessedImpact(thiefData.snapshot, request.impactId);
    stampUpdate(victimData.snapshot, now);
    stampUpdate(thiefData.snapshot, now);

    tx.set(thiefData.ref, thiefData.snapshot);
    tx.set(victimData.ref, victimData.snapshot);

    // Partial = thief got less than requested either because the victim was
    // too poor OR because a shield absorbed the strike. Both flow through
    // the same "AppliedPartially" status so the existing client codepath
    // (toast "you only got X of Y") works without a new status code.
    const status =
        thiefGain < requested * safeBonusMultiplier
            ? AuthoritativeStealStatus.AppliedPartially
            : AuthoritativeStealStatus.Success;

    // stolenAmount reflects what the THIEF received (after multiplier and
    // any shield deflection). The UI floats "+X" over the doll using this
    // value to celebrate the thief's actual gain.
    return {
        status,
        thiefSnapshot: thiefData.snapshot,
        victimSnapshot: victimData.snapshot,
        stolenAmount: thiefGain,
        message: "",
    };
}
