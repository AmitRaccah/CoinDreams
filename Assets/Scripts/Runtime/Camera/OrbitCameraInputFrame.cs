using UnityEngine;

namespace Game.Runtime.Cameras
{
    public readonly struct OrbitCameraInputFrame
    {
        public static readonly OrbitCameraInputFrame None = new OrbitCameraInputFrame(false, Vector2.zero);

        public OrbitCameraInputFrame(bool hasInput, Vector2 delta)
        {
            HasInput = hasInput;
            Delta = delta;
        }

        public bool HasInput { get; }
        public Vector2 Delta { get; }
    }
}
