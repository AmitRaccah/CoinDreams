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
    const stolen = Math.min(requested, victimCoins);

    if (stolen <= 0) {
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

    const thiefGain = stolen * safeBonusMultiplier;

    victimData.snapshot.coins -= stolen;
    thiefData.snapshot.coins += thiefGain;

    stampProcessedImpact(victimData.snapshot, request.impactId);
    stampProcessedImpact(thiefData.snapshot, request.impactId);
    stampUpdate(victimData.snapshot, now);
    stampUpdate(thiefData.snapshot, now);

    tx.set(thiefData.ref, thiefData.snapshot);
    tx.set(victimData.ref, victimData.snapshot);

    const status =
        stolen < requested
            ? AuthoritativeStealStatus.AppliedPartially
            : AuthoritativeStealStatus.Success;

    // stolenAmount reflects what the THIEF received (multiplied), since the UI
    // shows the floating "+X" over the doll to celebrate the thief's gain.
    return {
        status,
        thiefSnapshot: thiefData.snapshot,
        victimSnapshot: victimData.snapshot,
        stolenAmount: thiefGain,
        message: "",
    };
}
