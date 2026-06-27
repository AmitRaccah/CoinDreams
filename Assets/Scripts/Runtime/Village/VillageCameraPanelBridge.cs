#nullable enable

using System;
using Cysharp.Threading.Tasks;
using Game.Signals;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace Game.Runtime.Village
{
    /// <summary>
    /// Decoupled bridge from the panel system to the build camera. Listens to
    /// the <see cref="PanelVisibilityChangedSignal"/> that <c>PanelNavigator</c>
    /// already publishes and drives the camera: entering the overview when the
    /// buildings panel opens, exiting to the city when it closes (or another
    /// panel takes over). No reference to the panel itself.
    ///
    /// A "drop-on" component on the camera controls object — injected via
    /// InjectAllInScenes from GameplayLifetimeScope, not registered as a service.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VillageCameraPanelBridge : MonoBehaviour
    {
        private const string BuildingsPanelKey = "buildings";

        private IVillageCameraDirector? director;
        private IDisposable? subscription;
        private bool wasBuildingsOpen;
        private readonly Action<Exception> logUnlessCanceled;

        public VillageCameraPanelBridge()
        {
            // Cached once so HandleVisibilityChanged allocates no per-event delegate.
            logUnlessCanceled = ex =>
            {
                if (ex is OperationCanceledException)
                {
                    return;
                }

                Debug.LogException(ex);
            };
        }

        [Inject]
        public void Construct(
            ISubscriber<PanelVisibilityChangedSignal> visibilitySubscriber,
            IVillageCameraDirector cameraDirector)
        {
            director = cameraDirector;
            subscription?.Dispose();
            subscription = visibilitySubscriber.Subscribe(HandleVisibilityChanged);
        }

        private void OnDestroy()
        {
            subscription?.Dispose();
            subscription = null;
        }

        private void HandleVisibilityChanged(PanelVisibilityChangedSignal signal)
        {
            bool isBuildings = signal.IsAnyPanelOpen
                && string.Equals(signal.CurrentPanelKey, BuildingsPanelKey, StringComparison.Ordinal);

            if (isBuildings && !wasBuildingsOpen)
            {
                wasBuildingsOpen = true;
                director?.EnterOverviewAsync().Forget(logUnlessCanceled);
            }
            else if (!isBuildings && wasBuildingsOpen)
            {
                wasBuildingsOpen = false;
                director?.ExitToCityAsync().Forget(logUnlessCanceled);
            }
        }
    }
}
