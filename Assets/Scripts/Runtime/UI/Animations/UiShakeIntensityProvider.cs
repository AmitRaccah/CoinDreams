#nullable enable

using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Runtime.UI.Animations
{
    /// <summary>
    /// Accumulates a "shake intensity" from rapid pointer presses and bleeds
    /// it back to zero while idle. Exposes the intensity as a multiplier
    /// (1 + intensity) through IUiAnimationSpeedModulator so the inner
    /// animation layers in the descendant hierarchy run faster the harder
    /// the player taps — the snow-globe shake effect.
    ///
    /// Per-press the intensity jumps by <c>intensityPerPress</c>; per second
    /// it bleeds by <c>decayPerSecond</c>. The hard cap <c>maxIntensity</c>
    /// prevents spam-tap runaway visuals.
    ///
    /// SRP: only owns the intensity number and its lifecycle. The consumers
    /// (rotators / pulsers / bobbers) depend on the interface, not on this
    /// class — swap a different modulator in and they adapt automatically.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiShakeIntensityProvider : MonoBehaviour,
        IPointerDownHandler, IUiAnimationSpeedModulator
    {
        [Tooltip("Intensity added per press. The exposed multiplier is 1 + intensity, " +
            "so 1.0 here means each tap adds 1× speed on top of ambient.")]
        [SerializeField, Range(0.1f, 3f)] private float intensityPerPress = 1f;

        [Tooltip("Hard ceiling on accumulated intensity. Caps the speed multiplier " +
            "at 1 + maxIntensity. Prevents runaway visuals from spam tapping.")]
        [SerializeField, Range(1f, 10f)] private float maxIntensity = 5f;

        [Tooltip("Intensity bled per second while idle. Higher = settles faster. " +
            "At max intensity, time-to-settle ≈ maxIntensity / decayPerSecond.")]
        [SerializeField, Range(0.1f, 10f)] private float decayPerSecond = 2.5f;

        private float intensity;

        public float CurrentMultiplier => 1f + intensity;

        public void OnPointerDown(PointerEventData eventData)
        {
            intensity = Mathf.Min(maxIntensity, intensity + intensityPerPress);
        }

        private void Update()
        {
            if (intensity <= 0f) return;
            intensity = Mathf.Max(0f, intensity - decayPerSecond * Time.unscaledDeltaTime);
        }
    }
}
