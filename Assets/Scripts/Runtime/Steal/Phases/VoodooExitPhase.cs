#nullable enable

using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Signals;
using MessagePipe;
using VContainer;

namespace Game.Runtime.Steal.Phases
{
    /// <summary>
    /// Exit phase of the voodoo-steal mini-game. Today it only publishes the
    /// session-ended signal — that's enough for the existing presenters
    /// (doll hides, victim-name label clears). The class is the future home
    /// of the exit cinematic: doll-shatter animation, exit sound, camera
    /// return-to-city kick-off, etc.
    ///
    /// SRP: only the exit sequence. The coordinator clears its session
    /// field before awaiting this so the IVoodooSessionStateReader contract
    /// is consistent throughout the cinematic ("session ended" = HasActive
    /// already false).
    /// </summary>
    public sealed class VoodooExitPhase
    {
        private readonly IPublisher<VoodooSessionEndedSignal> sessionEndedPublisher;

        [Inject]
        public VoodooExitPhase(IPublisher<VoodooSessionEndedSignal> sessionEndedPublisher)
        {
            this.sessionEndedPublisher = sessionEndedPublisher;
        }

        public UniTask RunAsync(string sessionId, int totalStolen, bool dollBroken, CancellationToken ct)
        {
            sessionEndedPublisher.Publish(new VoodooSessionEndedSignal(sessionId, totalStolen, dollBroken));
            // No animation yet — return synchronously. Once cinematic hooks
            // arrive they become `await` statements right here; callers
            // already await this UniTask so nothing changes for them.
            return UniTask.CompletedTask;
        }
    }
}
