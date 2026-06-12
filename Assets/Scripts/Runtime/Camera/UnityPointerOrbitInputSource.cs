#nullable enable

using UnityEngine;

namespace Game.Runtime.Cameras
{
    [DisallowMultipleComponent]
    public sealed class UnityPointerOrbitInputSource
        : PointerDragInputSource<OrbitCameraInputFrame>, ICameraOrbitInputSource
    {
        protected override OrbitCameraInputFrame NoneFrame => OrbitCameraInputFrame.None;

        protected override OrbitCameraInputFrame CreateFrame(Vector2 delta) =>
            new OrbitCameraInputFrame(true, delta);
    }
}
