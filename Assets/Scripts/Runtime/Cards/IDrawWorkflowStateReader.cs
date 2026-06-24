#nullable enable

using System;

namespace Game.Runtime.Cards
{
    /// <summary>
    /// Read-only view of the card-draw workflow state machine. Consumers
    /// react to transitions without depending on the controller MonoBehaviour
    /// or the state-machine implementation. Idempotent: a no-op transition
    /// (same state requested twice) does NOT fire <see cref="StateChanged"/>.
    /// </summary>
    public interface IDrawWorkflowStateReader
    {
        CardDrawWorkflowState CurrentState { get; }

        /// <summary>
        /// Fires after the state has been updated to <paramref name="next"/>.
        /// Subscribers that need the previous state cache it themselves on
        /// each call — the signature stays single-arg to avoid forcing a
        /// (prev, next) tuple on consumers that don't need both.
        /// </summary>
        event Action<CardDrawWorkflowState>? StateChanged;
    }
}
