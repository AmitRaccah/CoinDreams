#nullable enable

using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game.Runtime.Village
{
    /// <summary>
    /// Single owner of the "build" camera flow. Drives the camera between the
    /// city (free, touch-enabled), the BUILD BUTTON VIEW overview, and a
    /// per-building focus pose — all locked (touch suspended) via the shared
    /// camera view-mode. Mirrors the proven card-board lock pattern in
    /// <c>DrawWorkflowExecutor</c>, scoped to the village.
    ///
    /// SRP — camera transitions + view-mode only. It knows nothing about
    /// upgrade economy or VFX timing (that is the choreographer's job).
    /// </summary>
    public interface IVillageCameraDirector
    {
        /// <summary>Save the current city pose and lock onto BUILD BUTTON VIEW.</summary>
        UniTask EnterOverviewAsync();

        /// <summary>Lock onto a per-building focus pose (designer-placed Empty).</summary>
        UniTask FocusAsync(Transform pose);

        /// <summary>Return from a building focus back to BUILD BUTTON VIEW.</summary>
        UniTask ReturnToOverviewAsync();

        /// <summary>Unlock: transition back to the saved city pose and hand control to touch.</summary>
        UniTask ExitToCityAsync();

        /// <summary>Resolve the focus point for a building id, or null if none is wired.</summary>
        BuildingUpgradeFocusPoint? GetFocusPoint(string buildingId);
    }
}
