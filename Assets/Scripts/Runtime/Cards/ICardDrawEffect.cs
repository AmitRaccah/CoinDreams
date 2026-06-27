#nullable enable

using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.Runtime.Cards
{
    /// <summary>
    /// A side effect of a card draw. The workflow executor filters effects
    /// via <see cref="ShouldRun"/>, runs every active effect's
    /// <see cref="PrepareAsync"/> in parallel with the card animation, then
    /// calls <see cref="Apply"/> for each at the end of the animation.
    ///
    /// SRP: each implementation handles ONE card type (steal, attack, bonus,
    /// jackpot, ...). OCP: new card types add a new implementation + DI
    /// registration; the executor doesn't change.
    ///
    /// Implementations may carry per-draw state between <see cref="PrepareAsync"/>
    /// and <see cref="Apply"/>. The workflow lock guarantees serial draws so
    /// reusing a single instance per effect type is safe.
    /// </summary>
    public interface ICardDrawEffect
    {
        /// <summary>
        /// Filter — return true if this effect applies to the given draw.
        /// Pure read, no side effects. Called once per draw before the
        /// prepare/apply pipeline runs.
        /// </summary>
        bool ShouldRun(in CardDrawContext context);

        /// <summary>
        /// Async work that runs in parallel with the card animation: server
        /// RPCs, asset loads, precomputation. Executor awaits this together
        /// with the animation lock before calling <see cref="Apply"/>.
        /// </summary>
        UniTask PrepareAsync(CardDrawContext context, CancellationToken ct);

        /// <summary>
        /// Visible commit — runs after the card animation lands AND prepare
        /// completes. Publish signals, mutate state, fire feedbacks.
        /// </summary>
        void Apply(in CardDrawContext context);
    }
}
