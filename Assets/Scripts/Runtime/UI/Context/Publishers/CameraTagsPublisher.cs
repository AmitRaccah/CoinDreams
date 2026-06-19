#nullable enable

using System;
using Game.Runtime.Cameras;
using VContainer;
using VContainer.Unity;

namespace Game.Runtime.UI.Context.Publishers
{
    /// <summary>
    /// Mirrors the <see cref="ICameraViewModeReader"/>'s current mode into
    /// exactly one of <see cref="UiTags.CameraCity"/>,
    /// <see cref="UiTags.CameraBoard"/>, or <see cref="UiTags.CameraTransitioning"/>.
    /// Lives in Gameplay scope because the camera reader does — the
    /// UiContextService it writes to is inherited from the persistent parent
    /// scope.
    ///
    /// SRP: only this translation.
    /// </summary>
    public sealed class CameraTagsPublisher : IInitializable, IDisposable
    {
        private readonly ICameraViewModeReader cameraReader;
        private readonly UiContextService context;
        private bool subscribed;

        [Inject]
        public CameraTagsPublisher(
            ICameraViewModeReader cameraReader,
            UiContextService context)
        {
            this.cameraReader = cameraReader;
            this.context = context;
        }

        public void Initialize()
        {
            cameraReader.ModeChanged += HandleModeChanged;
            subscribed = true;
            // Push the starting mode immediately so binders that come up
            // before the first ModeChanged event still see a correct tag.
            HandleModeChanged(cameraReader.CurrentMode);
        }

        public void Dispose()
        {
            if (subscribed)
            {
                cameraReader.ModeChanged -= HandleModeChanged;
                subscribed = false;
            }
        }

        private void HandleModeChanged(CameraViewMode mode)
        {
            // Exactly one camera tag is true at any time. Setting/clearing
            // all three on every change keeps the model trivially consistent
            // and the cost is one HashSet probe per tag.
            context.SetTag(UiTags.CameraCity, mode == CameraViewMode.City);
            context.SetTag(UiTags.CameraBoard, mode == CameraViewMode.Board);
            context.SetTag(UiTags.CameraTransitioning, mode == CameraViewMode.Transitioning);
        }
    }
}
