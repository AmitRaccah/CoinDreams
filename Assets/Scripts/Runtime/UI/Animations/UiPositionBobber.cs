#nullable enable

using UnityEngine;

namespace Game.Runtime.UI.Animations
{
    /// <summary>
    /// Bobs the anchored position of a RectTransform around its captured
    /// edit-time anchored position using a sin wave. The tick rate is scaled
    /// by any <see cref="IUiAnimationSpeedModulator"/> in the parent
    /// hierarchy, and the amplitude itself grows with shake intensity when
    /// <c>shakeAmplitudeBoost</c> &gt; 0 — so sparkles fly further when the
    /// player is rapidly tapping.
    ///
    /// Implementation note: phase is accumulated manually so multiplier
    /// changes don't cause visible phase jumps.
    ///
    /// SRP: only moves anchored position.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class UiPositionBobber : MonoBehaviour
    {
        [SerializeField] private float frequencyHz = 0.5f;
        [SerializeField] private Vector2 amplitude = new Vector2(0f, 4f);
        [SerializeField, Range(0f, 1f)] private float phaseOffset01 = 0f;

        [Tooltip("If > 0, amplitude scales with shake intensity: " +
            "effective = amplitude * (1 + shakeAmplitudeBoost * (multiplier - 1)). " +
            "Use ~0.5 for sparkle layers so they fly further when shaken.")]
        [SerializeField, Range(0f, 2f)] private float shakeAmplitudeBoost = 0f;

        private RectTransform? rectTransform;
        private IUiAnimationSpeedModulator? speedModulator;
        private Vector2 baseAnchoredPosition;
        private float accumulatedCycles;

        private void Awake()
        {
            rectTransform = (RectTransform)transform;
            baseAnchoredPosition = rectTransform.anchoredPosition;
            speedModulator = GetComponentInParent<IUiAnimationSpeedModulator>();
            accumulatedCycles = phaseOffset01;
        }

        private void Update()
        {
            if (rectTransform == null) return;

            float multiplier = speedModulator?.CurrentMultiplier ?? 1f;
            accumulatedCycles += frequencyHz * multiplier * Time.unscaledDeltaTime;
            float phase = accumulatedCycles * Mathf.PI * 2f;
            float wave = Mathf.Sin(phase);

            float amplitudeScale = 1f + shakeAmplitudeBoost * Mathf.Max(0f, multiplier - 1f);
            rectTransform.anchoredPosition = baseAnchoredPosition + amplitude * amplitudeScale * wave;
        }
    }
}
