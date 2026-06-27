#nullable enable

using System;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game.Runtime.Village
{
    /// <inheritdoc cref="IBuildingUpgradeChoreographer"/>
    public sealed class BuildingUpgradeChoreographer : IBuildingUpgradeChoreographer
    {
        private readonly IVillageCameraDirector cameraDirector;

        public BuildingUpgradeChoreographer(IVillageCameraDirector cameraDirector)
        {
            this.cameraDirector = cameraDirector;
        }

        public async UniTask RunAsync(string buildingId, Func<Task> upgradeAction)
        {
            if (upgradeAction == null)
            {
                return;
            }

            BuildingUpgradeFocusPoint? focusPoint = cameraDirector?.GetFocusPoint(buildingId);
            if (cameraDirector == null || focusPoint == null)
            {
                // No camera/VFX wired for this building — just run the upgrade.
                await upgradeAction();
                return;
            }

            try
            {
                await cameraDirector.FocusAsync(focusPoint.Pose);
                focusPoint.PlayVfx();

                // Start the (networked) upgrade now; the level swap lands inside
                // it, under the smoke. Hold for the VFX length AND until the
                // upgrade has applied — WhenAll waits for the later of the two,
                // so the camera never leaves before the new level is shown.
                Task upgrade = upgradeAction();
                float dwell = focusPoint.VfxDuration;
                if (dwell > 0f)
                {
                    await UniTask.WhenAll(
                        UniTask.Delay(TimeSpan.FromSeconds(dwell), DelayType.DeltaTime, PlayerLoopTiming.Update),
                        upgrade.AsUniTask());
                }
                else
                {
                    await upgrade;
                }

                await cameraDirector.ReturnToOverviewAsync();
            }
            catch (OperationCanceledException)
            {
                // Interrupted (e.g. the panel closed mid-flight). The director /
                // panel bridge owns the resulting camera state — don't fight it.
            }
            catch (Exception ex)
            {
                Debug.LogError("[BuildingUpgradeChoreographer] Upgrade choreography failed: " + ex);
                // Best-effort return to the overview since the panel is still open.
                try
                {
                    await cameraDirector.ReturnToOverviewAsync();
                }
                catch (OperationCanceledException)
                {
                }
            }
        }
    }
}
