namespace Game.Runtime.Cameras
{
    public interface ICameraOrbitInputSource
    {
        OrbitCameraInputFrame ReadFrame();
        void CancelGesture();
    }
}
