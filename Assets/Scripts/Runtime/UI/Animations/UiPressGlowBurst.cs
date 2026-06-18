#nullable enable

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Runtime.UI.Animations
{
    /// <summary>
    /// On PointerDown, enables a target glow GameObject, sets its Image alpha
    /// to <c>fullAlpha</c>, and fades it to zero over <c>fadeDurationSec</c>
    /// while optionally expanding its scale outward. When the fade completes,
    /// the target is disabled again so it doesn't contribute to draw cost or
    /// raycast hits when idle.
    ///
    /// Rapid presses are handled by snapping alpha back to <c>fullAlpha</c>
    /// each PointerDown — the glow "re-bursts" instead of waiting for the
    /// previous fade to finish.
    ///
    /// Critical: fires on raw pointer events, so the glow flashes on every
    /// tap regardless of whether the underlying Button click actually leads
    /// to a card draw. That's the point: tactile feedback even during camera
    /// transitions / locked states.
    ///
    /// SRP: only handles the glow burst. The glow target is a separate Image,
    /// typically a child that's disabled by default at edit-time. Do not
    /// stack a UiAlphaPulser on the same glow target — they'd fight over the
    /// alpha channel; the burst owns it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiPressGlowBurst : MonoBehaviour, IPointerDownHandler
    {
        [Tooltip("The disabled outer-glow GameObject that lights up on each press.")]
        [SerializeField] private GameObject? glowTarget;
        [SerializeField, Range(0.05f, 2f)] private float fadeDurationSec = 0.45f;
        [SerializeField, Range(0f, 1f)] private float fullAlpha = 1f;
        [Tooltip("How much the glow expands outward during the fade. 0 = no expand.")]
        [SerializeField, Range(0f, 0.5f)] private float expandAmount = 0.15f;

        private Image? glowImage;
        private RectTransform? glowRect;
        private Vector3 glowBaseScale = Vector3.one;
        private float fadeStartTime;
        private bool fading;

        private void Awake()
        {
            if (glowTarget == null) return;

            glowImage = glowTarget.GetComponent<Image>();
            glowRect = glowTarget.GetComponent<RectTransform>();
            if (glowRect != null) glowBaseScale = glowRect.localScale;

            // Enforce the "disabled at rest" contract — even if the editor
            // state drifted, runtime starts clean.
            glowTarget.SetActive(false);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (glowTarget == null || glowImage == null) return;

            glowTarget.SetActive(true);
            SetAlpha(fullAlpha);
            if (glowRect != null) glowRect.localScale = glowBaseScale;
            fadeStartTime = Time.unscaledTime;
            fading = true;
        }

        private void Update()
        {
            if (!fading || glowTarget == null || glowImage == null) return;

            float elapsed = Time.unscaledTime - fadeStartTime;
            float t = fadeDurationSec > 0f ? Mathf.Clamp01(elapsed / fadeDurationSec) : 1f;

            // Alpha eases out so the glow lingers a touch before vanishing.
            float alphaEase = 1f - (1f - t) * (1f - t);
            SetAlpha(Mathf.Lerp(fullAlpha, 0f, alphaEase));

            if (glowRect != null && expandAmount > 0f)
            {
                float k = 1f + t * expandAmount;
                glowRect.localScale = glowBaseScale * k;
            }

            if (t >= 1f)
            {
                fading = false;
                glowTarget.SetActive(false);
            }
        }

        private void SetAlpha(float alpha)
        {
            if (glowImage == null) return;
            Color c = glowImage.color;
            if (Mathf.Approximately(c.a, alpha)) return;
            c.a = alpha;
            glowImage.color = c;
        }
    }
}
