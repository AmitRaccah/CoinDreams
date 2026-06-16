#nullable enable

using Game.Composition.Signals;
using Game.Domain.Steal;
using MessagePipe;

namespace Game.Runtime.Steal
{
    public sealed class VoodooStealCardLauncher : IStealCardLauncher
    {
        private readonly IPublisher<StealCardTriggeredSignal> publisher;

        public VoodooStealCardLauncher(IPublisher<StealCardTriggeredSignal> publisher)
        {
            this.publisher = publisher ?? throw new System.ArgumentNullException(nameof(publisher));
        }

        public void Launch(string triggerId)
        {
            // Publish a signal carrying the triggerId. Empty triggerId is allowed — the launcher
            // doesn't gate on payload; the coordinator decides whether to begin a voodoo session.
            publisher.Publish(new StealCardTriggeredSignal(triggerId ?? string.Empty));
        }
    }
}
