#nullable enable

using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Domain.Player.Voodoo;
using Game.Runtime.Steal.Phases;

namespace Game.Runtime.Cards
{
    /// <summary>
    /// Steal-card effect. <see cref="PrepareAsync"/> runs the BeginVoodooSession
    /// RPC in parallel with the card animation; <see cref="Apply"/> publishes
    /// the session-started signal once the card has landed so the doll
    /// appears the instant the visual completes (no extra wait on the
    /// server roundtrip).
    ///
    /// State (the prepared session) is held between Prepare and Apply.
    /// Safe because the draw workflow lock serialises draws end-to-end.
    /// </summary>
    public sealed class StealCardEffect : ICardDrawEffect
    {
        private readonly VoodooEntryPhase entryPhase;
        private VoodooSession? prepared;

        public StealCardEffect(VoodooEntryPhase entryPhase)
        {
            this.entryPhase = entryPhase;
        }

        public bool ShouldRun(in CardDrawContext context)
        {
            var result = context.Result;
            return result != null
                && result.IsSuccess
                && !string.IsNullOrEmpty(result.StealTriggerId);
        }

        public async UniTask PrepareAsync(CardDrawContext context, CancellationToken ct)
        {
            prepared = await entryPhase.BeginAsync(context.Multiplier, ct);
        }

        public void Apply(in CardDrawContext context)
        {
            if (prepared == null) return;
            entryPhase.PublishStarted(prepared);
            prepared = null;
        }
    }
}
