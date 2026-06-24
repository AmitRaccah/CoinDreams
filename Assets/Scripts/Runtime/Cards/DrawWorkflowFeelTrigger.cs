#nullable enable

using System;
using MoreMountains.Feedbacks;
using UnityEngine;
using VContainer;

namespace Game.Runtime.Cards
{
    /// <summary>
    /// Bridges card-draw workflow state transitions to Feel chains. Each
    /// binding fires its <see cref="StateBinding.OnEnter"/> chain when the
    /// workflow transitions INTO the configured state, and its
    /// <see cref="StateBinding.OnExit"/> chain when it transitions OUT.
    /// Visual work (SetActive, Button.interactable, scale, sound) lives
    /// inside the MMF chains — this component is a pure dispatcher.
    ///
    /// Subscribes during <c>Construct</c> (VContainer post-injection) and
    /// unsubscribes in <c>OnDestroy</c>. Deliberately NOT OnEnable/OnDisable —
    /// the dispatcher must stay live through Feel-driven SetActive(false) on
    /// any parent in its hierarchy; otherwise the "exit" chain that re-shows
    /// us would lose its subscriber the moment we hide.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DrawWorkflowFeelTrigger : MonoBehaviour
    {
        [Serializable]
        public struct StateBinding
        {
            [Tooltip("Workflow state this binding reacts to.")]
            public CardDrawWorkflowState State;

            [Tooltip("Played the frame the workflow enters this state.")]
            public MMF_Player? OnEnter;

            [Tooltip("Played the frame the workflow leaves this state.")]
            public MMF_Player? OnExit;
        }

        [Tooltip("Per-state Feel mappings. Order doesn't matter; only one " +
            "binding per state is honored (first match wins on lookup). " +
            "Empty OnEnter/OnExit slots are silently skipped.")]
        [SerializeField] private StateBinding[] bindings = Array.Empty<StateBinding>();

        private IDrawWorkflowStateReader? stateReader;
        private CardDrawWorkflowState lastState;

        [Inject]
        public void Construct(IDrawWorkflowStateReader stateReader)
        {
            this.stateReader = stateReader;
            this.lastState = stateReader.CurrentState;
            stateReader.StateChanged += HandleStateChanged;
        }

        private void OnDestroy()
        {
            if (stateReader != null)
            {
                stateReader.StateChanged -= HandleStateChanged;
                stateReader = null;
            }
        }

        private void HandleStateChanged(CardDrawWorkflowState next)
        {
            FindChain(lastState, enter: false)?.PlayFeedbacks();
            FindChain(next, enter: true)?.PlayFeedbacks();
            lastState = next;
        }

        // Linear scan. The bindings array is bounded by the enum's value
        // count (5) so a Dictionary would cost more than it saves — both
        // the constant-time win and the per-instance allocation.
        private MMF_Player? FindChain(CardDrawWorkflowState state, bool enter)
        {
            for (int i = 0; i < bindings.Length; i++)
            {
                if (bindings[i].State != state) continue;
                return enter ? bindings[i].OnEnter : bindings[i].OnExit;
            }
            return null;
        }
    }
}
