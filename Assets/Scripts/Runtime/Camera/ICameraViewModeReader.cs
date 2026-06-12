using System;

namespace Game.Runtime.Cameras
{
    public interface ICameraViewModeReader
    {
        event Action<CameraViewMode> ModeChanged;

        CameraViewMode CurrentMode { get; }
        bool IsCityView { get; }
    }
}
