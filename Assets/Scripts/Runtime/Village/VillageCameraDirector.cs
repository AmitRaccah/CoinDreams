#nullable enable

using Cysharp.Threading.Tasks;
using Game.Runtime.Cameras;
using Game.Runtime.Cards;
using UnityEngine;
using VContainer;

namespace Game.Runtime.Village
{
    /// <summary>
    /// Scene-side owner of the build camera flow. Lives on the camera controls
    /// object (next to <see cref="BuildingFocusRegistry"/>). Holds the single
    /// BUILD BUTTON VIEW anchor and drives the locked transitions; per-building
    /// focus poses come from the registry (resolved as a sibling component).
    ///
    /// While in any non-City mode the <c>MapOrbitCameraController</c> stays
    /// idle, so the transition service owns the transform and touch is
    /// suspended for the whole build session — by design.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VillageCameraDirector : MonoBehaviour, IVillageCameraDirector
    {
        [Tooltip("The BUILD BUTTON VIEW pose — a CameraPoseAnchor placed in front of the buildings. " +
            "The camera locks here while the buildings panel is open.")]
        [SerializeField] private CameraPoseAnchor? overviewAnchor;

        [Inject] private ICameraTransitionService? transition;
        [Inject] private ICameraViewModeWriter? modeWriter;
        [Inject] private ICameraViewModeReader? modeReader;

        private BuildingFocusRegistry? registry;
        private bool registryResolved;
        private CameraPose? lastCityPose;

        public async UniTask EnterOverviewAsync()
        {
            if (transition == null || overviewAnchor == null)
            {
                Debug.LogWarning("[VillageCameraDirector] Missing transition service or overview anchor.", this);
                modeWriter?.SetMode(CameraViewMode.Building);
                return;
            }

            // Capture the city pose only when coming from the free city view,
            // so a re-entry (return-to-overview after a focus) never clobbers
            // the pose we must restore on exit.
            if (modeReader == null || modeReader.IsCityView)
            {
                lastCityPose = transition.CurrentPose;
            }

            modeWriter?.SetMode(CameraViewMode.Transitioning);
            await transition.StartTransitionAsync(overviewAnchor.transform);
            modeWriter?.SetMode(CameraViewMode.Building);
        }

        public async UniTask FocusAsync(Transform pose)
        {
            if (transition == null || pose == null)
            {
                return;
            }

            modeWriter?.SetMode(CameraViewMode.Transitioning);
            await transition.StartTransitionAsync(pose);
            modeWriter?.SetMode(CameraViewMode.Building);
        }

        public async UniTask ReturnToOverviewAsync()
        {
            if (transition == null || overviewAnchor == null)
            {
                return;
            }

            modeWriter?.SetMode(CameraViewMode.Transitioning);
            await transition.StartTransitionAsync(overviewAnchor.transform);
            modeWriter?.SetMode(CameraViewMode.Building);
        }

        public async UniTask ExitToCityAsync()
        {
            if (transition == null)
            {
                modeWriter?.SetMode(CameraViewMode.City);
                return;
            }

            modeWriter?.SetMode(CameraViewMode.Transitioning);
            try
            {
                if (lastCityPose.HasValue)
                {
                    await transition.StartTransitionAsync(lastCityPose.Value);
                }
            }
            finally
            {
                // Always hand control back to touch, even if the transition was
                // interrupted — otherwise the camera would stay input-locked.
                lastCityPose = null;
                modeWriter?.SetMode(CameraViewMode.City);
            }
        }

        public BuildingUpgradeFocusPoint? GetFocusPoint(string buildingId)
        {
            EnsureRegistry();
            if (registry == null || string.IsNullOrEmpty(buildingId))
            {
                return null;
            }

            return registry.TryGet(buildingId, out BuildingUpgradeFocusPoint focusPoint)
                ? focusPoint
                : null;
        }

        private void EnsureRegistry()
        {
            if (registryResolved)
            {
                return;
            }

            registryResolved = true;
            registry = GetComponent<BuildingFocusRegistry>();
            if (registry == null)
            {
                registry = FindAnyObjectByType<BuildingFocusRegistry>();
            }
        }
    }
}
