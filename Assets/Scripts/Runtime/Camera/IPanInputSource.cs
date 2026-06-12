namespace Game.Runtime.Cameras
{
    public interface IPanInputSource
    {
        PanInputFrame ReadFrame();
        void CancelGesture();
    }
}
