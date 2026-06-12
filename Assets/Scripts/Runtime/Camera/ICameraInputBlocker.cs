using UnityEngine;

namespace Game.Runtime.Cameras
{
    public interface ICameraInputBlocker
    {
        bool IsBlocked(Vector2 screenPosition);
    }
}
