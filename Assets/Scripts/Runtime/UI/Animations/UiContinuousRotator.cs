#nullable enable

using UnityEngine;

namespace Game.Runtime.UI.Animations
{
    /// <summary>
    /// Rotates a RectTransform continuously around its Z axis at
    /// <c>degreesPerSecond</c>, scaled by any <see cref="IUiAnimationSpeedModulator"/>
    /// found in the parent hierarchy. Intended for layered UI effects like
    /// mist halos that drift around a focal point.
    ///
    /// SRP: only does rotation. Compose with UiAlphaPulser / UiPulseScaler
    /// on the same GameObject for layered effects without coupling either
    /// class.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class UiContinuousRotator : MonoBehaviour
    {
        [SerializeField] private float degreesPerSecond = 6f;

        private RectTransform? rectTransform;
        private IUiAnimationSpeedModulator? speedModulator;

        private void Awake()
        {
            rectTransform = (RectTransform)transform;
            speedModulator = GetComponentInParent<IUiAnimationSpeedModulator>();
        }

        private void Update()
        {
            if (rectTransform == null) return;
            float multiplier = speedModulator?.CurrentMultiplier ?? 1f;
            float delta = degreesPerSecond * multiplier * Time.unscaledDeltaTime;
            rectTransform.Rotate(0f, 0f, delta, Space.Self);
        }
    }
}
