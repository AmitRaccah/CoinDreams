namespace Game.Runtime.Cameras
{
    public enum CameraViewMode
    {
        City = 0,
        Transitioning = 1,
        Board = 2,

        // Village "build" flow lock (BUILD BUTTON VIEW + per-building focus).
        // Like Board it is a non-City mode, so MapOrbitCameraController stays
        // idle and the transition service owns the transform — touch is
        // suspended for the whole build session. Distinct value only for
        // readability; the controller special-cases City alone.
        Building = 3
    }
}
