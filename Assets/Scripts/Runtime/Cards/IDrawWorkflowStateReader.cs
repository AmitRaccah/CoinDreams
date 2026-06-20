#nullable enable

using System;

namespace Game.Runtime.Cards
{
    /// <summary>
    /// Read-only view of the card-draw workflow state machine. Lets
    /// observers (tag publishers, telemetry, debug overlays) react to
    /// state changes without taking a dependency on the controller
    /// MonoBehaviour or on the state-machine implementation itself.
    /// </summary>
    public interface IDrawWorkflowStateReader
    {
        CardDrawWorkflowState CurrentState { get; }
        event Action<CardDrawWorkflowState>? StateChanged;
    }
}
