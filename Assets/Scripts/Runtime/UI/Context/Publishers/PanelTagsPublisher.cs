#nullable enable

using System;
using Game.Composition.Signals;
using MessagePipe;
using VContainer;
using VContainer.Unity;

namespace Game.Runtime.UI.Context.Publishers
{
    /// <summary>
    /// Translates <see cref="PanelVisibilityChangedSignal"/> into UI context
    /// tags. Sets <see cref="UiTags.PanelOpen"/> while any panel is up, plus
    /// a per-key tag like "panel-buildings" so binders can react to a
    /// specific panel without grepping signal payloads.
    ///
    /// SRP: only this translation. The navigator decides what's open; the
    /// binders decide what to do; this class just speaks the shared language.
    /// </summary>
    public sealed class PanelTagsPublisher : IInitializable, IDisposable
    {
        private readonly ISubscriber<PanelVisibilityChangedSignal> subscriber;
        private readonly UiContextService context;

        private IDisposable? subscription;
        private string lastPerKeyTag = string.Empty;

        [Inject]
        public PanelTagsPublisher(
            ISubscriber<PanelVisibilityChangedSignal> subscriber,
            UiContextService context)
        {
            this.subscriber = subscriber;
            this.context = context;
        }

        public void Initialize()
        {
            subscription = subscriber.Subscribe(HandleVisibilityChanged);
        }

        public void Dispose()
        {
            subscription?.Dispose();
            subscription = null;
        }

        private void HandleVisibilityChanged(PanelVisibilityChangedSignal signal)
        {
            // Clear the previous per-key tag so swapping from buildings →
            // attack doesn't leave "panel-buildings" stuck on.
            if (!string.IsNullOrEmpty(lastPerKeyTag))
            {
                context.SetTag(lastPerKeyTag, false);
                lastPerKeyTag = string.Empty;
            }

            context.SetTag(UiTags.PanelOpen, signal.IsAnyPanelOpen);

            if (signal.IsAnyPanelOpen && !string.IsNullOrEmpty(signal.CurrentPanelKey))
            {
                string perKeyTag = UiTags.PanelKeyPrefix + signal.CurrentPanelKey;
                context.SetTag(perKeyTag, true);
                lastPerKeyTag = perKeyTag;
            }
        }
    }
}
