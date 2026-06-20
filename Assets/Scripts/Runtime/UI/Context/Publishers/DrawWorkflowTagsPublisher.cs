#nullable enable

using System;
using Game.Runtime.Cards;
using VContainer;
using VContainer.Unity;

namespace Game.Runtime.UI.Context.Publishers
{
    /// <summary>
    /// Mirrors the card-draw workflow state into the
    /// <see cref="UiTags.DrawEngaged"/> tag: true the moment the workflow
    /// leaves Idle (player clicked Draw), false again when it returns to
    /// Idle (player back in the city). This lets buttons hide on the
    /// FIRST frame of the camera transition rather than after it lands —
    /// closes the race-window where the player could open another panel
    /// mid-flight.
    ///
    /// SRP: only this translation. The state machine owns the workflow;
    /// the binders react to the tag. This publisher knows neither.
    /// </summary>
    public sealed class DrawWorkflowTagsPublisher : IInitializable, IDisposable
    {
        private readonly IDrawWorkflowStateReader stateReader;
        private readonly UiContextService context;
        private bool subscribed;

        [Inject]
        public DrawWorkflowTagsPublisher(
            IDrawWorkflowStateReader stateReader,
            UiContextService context)
        {
            this.stateReader = stateReader;
            this.context = context;
        }

        public void Initialize()
        {
            stateReader.StateChanged += HandleStateChanged;
            subscribed = true;
            // Push the starting state immediately so binders that come up
            // before the first transition still see a correct tag value.
            HandleStateChanged(stateReader.CurrentState);
        }

        public void Dispose()
        {
            if (subscribed)
            {
                stateReader.StateChanged -= HandleStateChanged;
                subscribed = false;
            }
        }

        private void HandleStateChanged(CardDrawWorkflowState state)
        {
            context.SetTag(UiTags.DrawEngaged, state != CardDrawWorkflowState.Idle);
        }
    }
}
