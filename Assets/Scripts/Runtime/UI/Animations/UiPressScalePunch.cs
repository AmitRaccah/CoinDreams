#nullable enable

using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Runtime.UI.Animations
{
    /// <summary>
    /// Tactile press feedback for any UI element that already receives
    /// pointer events. On PointerDown, eases the local scale down to
    /// <c>pressedScale</c>; on PointerUp / PointerExit, eases it back to 1
    /// with a back-out overshoot for a satisfying "boing".
    ///
    /// Critical: this component fires on raw pointer events, NOT on
    /// Button.onClick. The feedback is therefore decoupled from any gating
    /// logic downstream (camera transition, energy depleted, etc.) — the
    /// press always feels good even when the underlying request is no-op'd.
    ///
    /// Exclusions: any RectTransform dragged into <c>excludedFromScale</c>
    /// has its localScale and localPosition inverse-corrected each frame so
    /// it appears visually static while its parent (this transform) pulses.
    /// Use for objects that conceptually shouldn't move with the press —
    /// e.g. a crystal-ball pedestal that the player is "holding" while the
    /// ball above it shakes. The exclusion only cancels scale; it does not
    /// cancel rotation. Excluded transforms MUST be DIRECT children of this
    /// transform (the inverse compensates for THIS transform's scale only —
    /// a deeper descendant would also pick up scaling from intermediate
    /// ancestors that this component doesn't see).
    ///
    /// SRP: only handles scale punch. Stack with UiPressGlowBurst for layered
    /// feedback. Do NOT stack with UiPulseScaler on the same GameObject —
    /// they'd both fight over <c>localScale</c>; put the ambient pulse on a
    /// child instead.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class UiPressScalePunch : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private enum AnimState { Idle, Pressing, Releasing }

        [SerializeField, Range(0.5f, 1f)] private float pressedScale = 0.92f;
        [SerializeField, Range(0.01f, 0.5f)] private float pressDurationSec = 0.05f;
        [SerializeField, Range(0.05f, 1f)] private float releaseDurationSec = 0.22f;
        [Tooltip("Standard easeOutBack overshoot constant; 1.7 ≈ default snappy bounce.")]
        [SerializeField, Range(0f, 3f)] private float overshootStrength = 1.7f;

        [Header("Exclusions")]
        [Tooltip("Descendant RectTransforms that should appear visually static during " +
            "the press scale. Each frame they get an inverse localScale and " +
            "inverse localPosition so the parent's scale is cancelled out " +
            "for them. Typical use: the pedestal of a crystal ball.")]
        [SerializeField] private RectTransform[] excludedFromScale = Array.Empty<RectTransform>();

        private RectTransform? rectTransform;
        private Vector3 baseScale;
        private AnimState state = AnimState.Idle;
        private float animStartTime;
        private float animStartK = 1f;
        private float animEndK = 1f;

        // Captured at Awake so the artist's edit-time values are the "rest"
        // pose. We use localPosition (not anchoredPosition) because anchored-
        // Position is an offset from an ANCHOR REFERENCE that itself moves
        // visually when the parent scales — inverse-scaling anchoredPosition
        // doesn't compensate for that anchor drift. localPosition is the
        // actual 3D offset from the parent pivot, so localPosition / S * S
        // cleanly cancels the parent scale for any anchor setup.
        // Length is locked at Awake — adding to excludedFromScale at runtime
        // won't be picked up. That's fine: the array is an editor-time
        // configuration, not a runtime hot-swap.
        private Vector3[] excludedBaseScales = Array.Empty<Vector3>();
        private Vector3[] excludedBaseLocalPositions = Array.Empty<Vector3>();

        private void Awake()
        {
            rectTransform = (RectTransform)transform;
            baseScale = rectTransform.localScale;

            int length = excludedFromScale != null ? excludedFromScale.Length : 0;
            excludedBaseScales = new Vector3[length];
            excludedBaseLocalPositions = new Vector3[length];
            for (int i = 0; i < length; i++)
            {
                RectTransform? rt = excludedFromScale![i];
                if (rt == null) continue;
                excludedBaseScales[i] = rt.localScale;
                excludedBaseLocalPositions[i] = rt.localPosition;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            StartAnim(AnimState.Pressing, pressedScale);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            StartAnim(AnimState.Releasing, 1f);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            // Finger dragged off without lifting — release anyway so we
            // don't leave the ball stuck in pressed state.
            if (state == AnimState.Pressing) StartAnim(AnimState.Releasing, 1f);
        }

        // Pick up from the current scale so rapid taps don't snap visually:
        // the new anim starts where the previous left off.
        private void StartAnim(AnimState newState, float endK)
        {
            if (rectTransform == null) return;
            state = newState;
            animStartTime = Time.unscaledTime;
            float baseX = Mathf.Approximately(baseScale.x, 0f) ? 1f : baseScale.x;
            animStartK = rectTransform.localScale.x / baseX;
            animEndK = endK;
        }

        private void Update()
        {
            if (state == AnimState.Idle || rectTransform == null) return;

            float duration = state == AnimState.Pressing
                ? pressDurationSec
                : releaseDurationSec;
            float elapsed = Time.unscaledTime - animStartTime;
            float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;

            float k;
            if (state == AnimState.Pressing)
            {
                k = Mathf.Lerp(animStartK, animEndK, t);
            }
            else
            {
                // easeOutBack overshoots above 1 around t≈0.7, settles to 1.
                float eased = EaseOutBack(t, overshootStrength);
                k = Mathf.LerpUnclamped(animStartK, animEndK, eased);
            }

            ApplyScale(k);

            if (t >= 1f)
            {
                state = AnimState.Idle;
                ApplyScale(animEndK);
            }
        }

        // Sets the parent scale and applies the inverse correction to every
        // excluded child. Skips the inverse when k is at or below epsilon to
        // avoid a 1/0 explosion if someone configures pressedScale = 0.
        // We assign localPosition (not anchoredPosition) so the cancellation
        // works for ANY anchor configuration — corner anchors, stretch
        // anchors, etc. localPosition / S * S = localPosition cleanly cancels
        // the parent scale regardless of where the child's anchor sits.
        private void ApplyScale(float k)
        {
            if (rectTransform == null) return;
            rectTransform.localScale = baseScale * k;

            if (k <= 0.0001f) return;
            float inverse = 1f / k;
            int count = Mathf.Min(excludedFromScale.Length, excludedBaseScales.Length);
            for (int i = 0; i < count; i++)
            {
                RectTransform? rt = excludedFromScale[i];
                if (rt == null) continue;
                rt.localScale = excludedBaseScales[i] * inverse;
                rt.localPosition = excludedBaseLocalPositions[i] * inverse;
            }
        }

        private static float EaseOutBack(float t, float overshoot)
        {
            float c1 = overshoot;
            float c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }
    }
}
