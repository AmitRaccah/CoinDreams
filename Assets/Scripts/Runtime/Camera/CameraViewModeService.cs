using System;

namespace Game.Runtime.Cameras
{
    public sealed class CameraViewModeService : ICameraViewModeReader, ICameraViewModeWriter
    {
        public event Action<CameraViewMode> ModeChanged = delegate { };

        public CameraViewMode CurrentMode { get; private set; } = CameraViewMode.City;

        public bool IsCityView
        {
            get { return CurrentMode == CameraViewMode.City; }
        }

        public void SetMode(CameraViewMode mode)
        {
            if (CurrentMode == mode)
            {
                return;
            }

            CurrentMode = mode;
            ModeChanged?.Invoke(mode);
        }
    }
}
