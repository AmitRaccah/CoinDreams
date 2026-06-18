#nullable enable

using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.UI.Animations
{
    /// <summary>
    /// Pulses the alpha channel of an Image's color using a sin wave clamped
    /// between minAlpha and maxAlpha. The tick rate is scaled by any
    /// <see cref="IUiAnimationSpeedModulator"/> in the parent hierarchy, so
    /// shaking the host (e.g. the crystal ball) speeds the breathing up.
    ///
    /// Implementation note: phase is accumulated manually so multiplier
    /// changes don't cause visible phase jumps.
    ///
    /// SRP: only does alpha. Skips assignment when alpha hasn't changed past
    /// epsilon to keep CanvasRenderer dirty-flag work quiet.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    public sealed class UiAlphaPulser : MonoBehaviour
    {
        [SerializeField] private float frequencyHz = 0.3f;
        [SerializeField, Range(0f, 1f)] private float minAlpha = 0.4f;
        [SerializeField, Range(0f, 1f)] private float maxAlpha = 1f;
        [SerializeField, Range(0f, 1f)] private float phaseOffset01 = 0f;

        private Image? image;
        private IUiAnimationSpeedModulator? speedModulator;
        private float accumulatedCycles;

        private void Awake()
        {
            image = GetComponent<Image>();
            speedModulator = GetComponentInParent<IUiAnimationSpeedModulator>();
            accumulatedCycles = phaseOffset01;
        }

        private void Update()
        {
            if (image == null) return;

            float multiplier = speedModulator?.CurrentMultiplier ?? 1f;
            accumulatedCycles += frequencyHz * multiplier * Time.unscaledDeltaTime;
            float phase = accumulatedCycles * Mathf.PI * 2f;
            float wave01 = (Mathf.Sin(phase) + 1f) * 0.5f;
            float alpha = Mathf.Lerp(minAlpha, maxAlpha, wave01);

            Color c = image.color;
            if (Mathf.Approximately(c.a, alpha)) return;
            c.a = alpha;
            image.color = c;
        }
    }
}
