#nullable enable

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Runtime.UI.Diagnostics
{
    /// <summary>
    /// Temporary diagnostic: drop on PanelHolder, run Play, drag inside the
    /// panel. The Console prints exactly which pointer events arrive, plus
    /// the ScrollRect / Image / EventSystem state. Use to localize which
    /// link in the input chain is dropping the drag.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScrollRectDragDebugger : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private void Start()
        {
            UnityEngine.Debug.Log("[ScrollDebug] === Start on '" + name + "' ===");

            EventSystem es = EventSystem.current;
            UnityEngine.Debug.Log("[ScrollDebug] EventSystem.current: " + (es != null ? es.name : "NULL — NO INPUT WILL WORK"));

            ScrollRect sr = GetComponent<ScrollRect>();
            if (sr == null)
            {
                UnityEngine.Debug.LogWarning("[ScrollDebug] No ScrollRect on this GameObject.");
            }
            else
            {
                UnityEngine.Debug.Log("[ScrollDebug] ScrollRect: horizontal=" + sr.horizontal
                    + ", vertical=" + sr.vertical + ", inertia=" + sr.inertia);
                UnityEngine.Debug.Log("[ScrollDebug]  content="
                    + (sr.content != null ? sr.content.name : "NULL")
                    + ", viewport=" + (sr.viewport != null ? sr.viewport.name : "NULL"));
                if (sr.content != null) UnityEngine.Debug.Log("[ScrollDebug]  content.rect.size=" + sr.content.rect.size);
                if (sr.viewport != null) UnityEngine.Debug.Log("[ScrollDebug]  viewport.rect.size=" + sr.viewport.rect.size);
            }

            Image img = GetComponent<Image>();
            if (img == null)
            {
                UnityEngine.Debug.LogWarning("[ScrollDebug] No Image on this GameObject — Raycast won't hit unless a child catches it.");
            }
            else
            {
                UnityEngine.Debug.Log("[ScrollDebug] Image: raycastTarget=" + img.raycastTarget + ", alpha=" + img.color.a);
            }

            CanvasGroup[] groups = GetComponentsInParent<CanvasGroup>(includeInactive: true);
            for (int i = 0; i < groups.Length; i++)
            {
                CanvasGroup g = groups[i];
                UnityEngine.Debug.Log("[ScrollDebug] CanvasGroup on '" + g.name
                    + "': interactable=" + g.interactable
                    + ", blocksRaycasts=" + g.blocksRaycasts
                    + ", alpha=" + g.alpha);
            }
        }

        public void OnPointerDown(PointerEventData e)
        {
            UnityEngine.Debug.Log("[ScrollDebug] PointerDown @ " + e.position
                + " | hit='" + (e.pointerCurrentRaycast.gameObject != null ? e.pointerCurrentRaycast.gameObject.name : "null") + "'");
        }
        public void OnPointerUp(PointerEventData e)
        {
            UnityEngine.Debug.Log("[ScrollDebug] PointerUp @ " + e.position);
        }
        public void OnBeginDrag(PointerEventData e)
        {
            UnityEngine.Debug.Log("[ScrollDebug] BeginDrag delta=" + e.delta);
        }
        public void OnDrag(PointerEventData e)
        {
            UnityEngine.Debug.Log("[ScrollDebug] Drag delta=" + e.delta);
        }
        public void OnEndDrag(PointerEventData e)
        {
            UnityEngine.Debug.Log("[ScrollDebug] EndDrag");
        }
    }
}
