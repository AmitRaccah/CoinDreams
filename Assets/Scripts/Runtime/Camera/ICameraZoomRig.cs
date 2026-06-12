namespace Game.Runtime.Cameras
{
    public interface ICameraZoomRig
    {
        void AddZoomDelta(float deltaSize);
        void Tick(float deltaTime);
        void CancelMotion();
    }
}
