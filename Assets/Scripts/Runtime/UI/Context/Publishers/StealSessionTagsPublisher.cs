#nullable enable

using System;
using Game.Composition.Signals;
using MessagePipe;
using VContainer;
using VContainer.Unity;

namespace Game.Runtime.UI.Context.Publishers
{
    /// <summary>
    /// Translates voodoo session lifecycle signals into the
    /// <see cref="UiTags.StealSession"/> tag. ON while a session is active,
    /// OFF when it ends — including cleanup paths like ProfileReplaced or a
    /// broken doll, since those publish VoodooSessionEndedSignal too.
    ///
    /// SRP: only this translation. The coordinator owns session state; this
    /// just mirrors "is a session up" into the tag bus.
    /// </summary>
    public sealed class StealSessionTagsPublisher : IInitializable, IDisposable
    {
        private readonly ISubscriber<VoodooSessionStartedSignal> startedSubscriber;
        private readonly ISubscriber<VoodooSessionEndedSignal> endedSubscriber;
        private readonly UiContextService context;

        private IDisposable? startedSubscription;
        private IDisposable? endedSubscription;

        [Inject]
        public StealSessionTagsPublisher(
            ISubscriber<VoodooSessionStartedSignal> startedSubscriber,
            ISubscriber<VoodooSessionEndedSignal> endedSubscriber,
            UiContextService context)
        {
            this.startedSubscriber = startedSubscriber;
            this.endedSubscriber = endedSubscriber;
            this.context = context;
        }

        public void Initialize()
        {
            startedSubscription = startedSubscriber.Subscribe(_ => context.SetTag(UiTags.StealSession, true));
            endedSubscription = endedSubscriber.Subscribe(_ => context.SetTag(UiTags.StealSession, false));
        }

        public void Dispose()
        {
            startedSubscription?.Dispose();
            startedSubscription = null;
            endedSubscription?.Dispose();
            endedSubscription = null;
        }
    }
}
