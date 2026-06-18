#nullable enable

using UnityEngine;

namespace Game.Runtime.UI.Animations
{
    /// <summary>
    /// Pulses the local scale of a RectTransform around its captured
    /// edit-time scale using a sin wave whose tick rate is scaled by any
    /// <see cref="IUiAnimationSpeedModulator"/> in the parent hierarchy.
    /// Phase offset lets stacked components avoid visible synchronization.
    ///
    /// Implementation note: phase is accumulated manually (not derived from
    /// Time.unscaledTime * freq) so multiplier changes from the modulator
    /// don't cause phase jumps mid-cycle.
    ///
    /// SRP: only does scale. The edit-time scale captured at Awake is the
    /// "rest" state — do not animate scale from a second source.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class UiPulseScaler : MonoBehaviour
    {
        [SerializeField] private float frequencyHz = 0.4f;
        [SerializeField] private float amplitude = 0.04f;
        [SerializeField, Range(0f, 1f)] private float phaseOffset01 = 0f;

        private RectTransform? rectTransform;
        private IUiAnimationSpeedModulator? speedModulator;
        private Vector3 baseScale;
        private float accumulatedCycles;

        private void Awake()
        {
            rectTransform = (RectTransform)transform;
            baseScale = rectTransform.localScale;
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
            float k = 1f + wave * amplitude;
            rectTransform.localScale = baseScale * k;
        }
    }
}
