#nullable enable

using UnityEngine;

namespace Game.Runtime.Cameras
{
    [DisallowMultipleComponent]
    public sealed class CameraPoseAnchor : MonoBehaviour, ICameraPoseProvider
    {
        [SerializeField] private bool includeOrthographicSize;
        [SerializeField] private float orthographicSize = 2.71f;

        public CameraPose GetPose()
        {
            return new CameraPose(
                transform.position,
                transform.rotation,
                includeOrthographicSize,
                orthographicSize);
        }
    }
}
