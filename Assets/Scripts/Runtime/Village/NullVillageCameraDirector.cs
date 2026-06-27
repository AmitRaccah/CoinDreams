#nullable enable

using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game.Runtime.Village
{
    /// <summary>
    /// No-op fallback so <see cref="IBuildingUpgradeChoreographer"/> and the
    /// panel bridge always resolve, even in scenes that don't wire a
    /// <see cref="VillageCameraDirector"/> (test/legacy scenes). The upgrade
    /// then runs without any camera choreography. Same opt-out shape as
    /// NullDrawCardPresentation.
    /// </summary>
    public sealed class NullVillageCameraDirector : IVillageCameraDirector
    {
        public UniTask EnterOverviewAsync() => UniTask.CompletedTask;

        public UniTask FocusAsync(Transform pose) => UniTask.CompletedTask;

        public UniTask ReturnToOverviewAsync() => UniTask.CompletedTask;

        public UniTask ExitToCityAsync() => UniTask.CompletedTask;

        public BuildingUpgradeFocusPoint? GetFocusPoint(string buildingId) => null;
    }
}
