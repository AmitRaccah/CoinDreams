#nullable enable

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Runtime.Cameras
{
    [DisallowMultipleComponent]
    public sealed class EventSystemCameraInputBlocker : MonoBehaviour, ICameraInputBlocker
    {
        private readonly List<RaycastResult> raycastResults = new List<RaycastResult>(8);
        private EventSystem? cachedEventSystem;
        private PointerEventData? pointerEventData;

        public bool IsBlocked(Vector2 screenPosition)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return false;
            }

            if (pointerEventData == null || cachedEventSystem != eventSystem)
            {
                cachedEventSystem = eventSystem;
                pointerEventData = new PointerEventData(eventSystem);
            }

            pointerEventData.Reset();
            pointerEventData.position = screenPosition;
            raycastResults.Clear();
            eventSystem.RaycastAll(pointerEventData, raycastResults);
            return raycastResults.Count > 0;
        }
    }
}
