#nullable enable

using System;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;

namespace Game.Runtime.Village
{
    /// <summary>
    /// Owns the per-upgrade sequence: fly the camera to the building, cover it
    /// with smoke, run the upgrade (the level swap happens under the smoke),
    /// hold for the VFX, then return to the overview. Timing only — it does not
    /// decide the new level (the runtime does) nor move the camera itself (the
    /// director does).
    /// </summary>
    public interface IBuildingUpgradeChoreographer
    {
        /// <summary>
        /// True while a choreography is mid-flight (camera flying / smoke /
        /// upgrade / returning). Callers gate re-entrant taps on this so a
        /// second upgrade can't start — and yank the camera to another
        /// building — before the current one returns to the overview.
        /// </summary>
        bool IsBusy { get; }

        /// <summary>
        /// Run the choreography around <paramref name="upgradeAction"/>. The
        /// action performs the authoritative upgrade (and applies the level);
        /// it is invoked once, inside the smoke window. Falls back to running
        /// the action plain when no focus point is wired for the building.
        ///
        /// Re-entrant calls (while <see cref="IsBusy"/> is true) are ignored.
        /// </summary>
        UniTask RunAsync(string buildingId, Func<Task> upgradeAction);
    }
}
