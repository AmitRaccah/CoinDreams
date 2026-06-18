#nullable enable

namespace Game.Runtime.UI.Animations
{
    /// <summary>
    /// A read-only source of an animation-speed multiplier that the rotator
    /// / pulser / bobber components query each frame. A value of 1.0 means
    /// "ambient" (no scaling); higher means the animation ticks faster.
    ///
    /// The animation components find the nearest implementation in their
    /// parent hierarchy at Awake. The interface decouples the consumers
    /// from any specific source — press-shake today, victory celebration
    /// or stun-tremor tomorrow, without touching the consumers (OCP).
    /// </summary>
    public interface IUiAnimationSpeedModulator
    {
        float CurrentMultiplier { get; }
    }
}
