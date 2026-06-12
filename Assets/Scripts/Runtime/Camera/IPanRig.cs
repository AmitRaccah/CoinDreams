namespace Game.Runtime.Cameras
{
    public interface IPanRig
    {
        void AddPanDelta(float deltaWorldUnits);
        void Tick(float deltaTime);
        void CancelMotion();
    }
}
