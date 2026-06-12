namespace Game.Runtime.Cameras
{
    public interface IOrbitCameraRig
    {
        void AddYawDelta(float deltaDegrees);
        void Tick(float deltaTime);
        void CancelMotion();
    }
}
