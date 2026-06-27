#nullable enable

using Game.Runtime.Cameras;
using MoreMountains.Feedbacks;
using UnityEngine;

namespace Game.Runtime.Village
{
    /// <summary>
    /// Designer-placed marker on the Empty inside each COMPLETE BUILDING prefab
    /// (the one holding levels 0-3). It is the single per-building authoring
    /// surface for the upgrade presentation:
    ///   • the camera pose to fly to  → its sibling <see cref="CameraPoseAnchor"/>
    ///   • the smoke/cover VFX         → <see cref="upgradeVfx"/>
    ///   • how long the camera dwells  → the VFX's own TotalDuration
    ///
    /// The building identity is read from the parent <see cref="BuildingVisualController"/>,
    /// so there is no duplicated building-id field to drift. The camera-side
    /// <see cref="BuildingFocusRegistry"/> discovers these at load and maps them
    /// by that id.
    ///
    /// SRP — per-building presentation refs + VFX playback only. It does not
    /// swap building levels (that stays in the upgrade runtime) and does not
    /// move the camera (that is the director).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CameraPoseAnchor))]
    public sealed class BuildingUpgradeFocusPoint : MonoBehaviour
    {
        [Tooltip("Smoke / cover VFX that masks the level swap. Plays when the camera arrives. " +
            "Its TotalDuration also defines how long the camera dwells here — no separate timer.")]
        [SerializeField] private MMF_Player? upgradeVfx;

        private string buildingId = string.Empty;
        private bool resolved;

        /// <summary>The transform the camera transitions to (its CameraPoseAnchor supplies the pose).</summary>
        public Transform Pose => transform;

        /// <summary>Owning building id, inherited from the parent building root.</summary>
        public string BuildingId
        {
            get
            {
                EnsureResolved();
                return buildingId;
            }
        }

        /// <summary>Camera dwell time = the VFX length. 0 when no VFX is wired.</summary>
        public float VfxDuration => upgradeVfx != null ? upgradeVfx.TotalDuration : 0f;

        public void PlayVfx()
        {
            if (upgradeVfx != null)
            {
                upgradeVfx.PlayFeedbacks();
            }
        }

        private void Awake() => EnsureResolved();

        private void EnsureResolved()
        {
            if (resolved)
            {
                return;
            }

            resolved = true;

            BuildingVisualController controller = GetComponentInParent<BuildingVisualController>(true);
            if (controller != null)
            {
                buildingId = controller.BuildingId;
            }
            else
            {
                Debug.LogWarning(
                    "[BuildingUpgradeFocusPoint] No BuildingVisualController found in parent — "
                    + "place this Empty inside the building prefab so its id can be resolved.",
                    this);
            }
        }
    }
}
