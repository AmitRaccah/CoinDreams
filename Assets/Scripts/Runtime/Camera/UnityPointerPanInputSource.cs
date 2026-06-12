#nullable enable

using UnityEngine;

namespace Game.Runtime.Cameras
{
    [DisallowMultipleComponent]
    public sealed class UnityPointerPanInputSource
        : PointerDragInputSource<PanInputFrame>, IPanInputSource
    {
        protected override PanInputFrame NoneFrame => PanInputFrame.None;

        protected override PanInputFrame CreateFrame(Vector2 delta) =>
            new PanInputFrame(true, delta);
    }
}
