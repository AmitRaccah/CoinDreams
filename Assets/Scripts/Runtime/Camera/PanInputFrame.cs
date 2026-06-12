using UnityEngine;

namespace Game.Runtime.Cameras
{
    public readonly struct PanInputFrame
    {
        public static readonly PanInputFrame None = new PanInputFrame(false, Vector2.zero);

        public PanInputFrame(bool hasInput, Vector2 delta)
        {
            HasInput = hasInput;
            Delta = delta;
        }

        public bool HasInput { get; }
        public Vector2 Delta { get; }
    }
}
