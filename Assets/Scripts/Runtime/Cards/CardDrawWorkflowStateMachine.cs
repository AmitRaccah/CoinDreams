#nullable enable

using System;

namespace Game.Runtime.Cards
{
    public enum DrawWorkflowAction
    {
        None = 0,
        MoveToBoard = 1,
        Draw = 2
    }

    public enum CardDrawWorkflowState
    {
        Idle = 0,
        MovingToBoard = 1,
        DrawMode = 2,
        Drawing = 3,
        ReturningToCity = 4
    }

    public sealed class CardDrawWorkflowStateMachine : IDrawWorkflowStateReader
    {
        public event Action<CardDrawWorkflowState>? StateChanged;

        public CardDrawWorkflowState CurrentState { get; private set; } = CardDrawWorkflowState.Idle;

        public DrawWorkflowAction HandleDrawClicked()
        {
            if (CurrentState == CardDrawWorkflowState.Idle)
            {
                Transition(CardDrawWorkflowState.MovingToBoard);
                return DrawWorkflowAction.MoveToBoard;
            }

            if (CurrentState == CardDrawWorkflowState.DrawMode)
            {
                Transition(CardDrawWorkflowState.Drawing);
                return DrawWorkflowAction.Draw;
            }

            return DrawWorkflowAction.None;
        }

        public bool TryBeginReturn()
        {
            if (CurrentState != CardDrawWorkflowState.DrawMode)
            {
                return false;
            }

            Transition(CardDrawWorkflowState.ReturningToCity);
            return true;
        }

        public void CompleteMoveToBoard(bool succeeded)
        {
            if (CurrentState != CardDrawWorkflowState.MovingToBoard)
            {
#if UNITY_EDITOR
                UnityEngine.Debug.LogWarning(
                    "[CardDrawWorkflowStateMachine] CompleteMoveToBoard ignored; current state is "
                        + CurrentState + ".");
#endif
                return;
            }

            Transition(succeeded
                ? CardDrawWorkflowState.DrawMode
                : CardDrawWorkflowState.Idle);
        }

        public void CompleteDraw()
        {
            if (CurrentState == CardDrawWorkflowState.Drawing)
            {
                Transition(CardDrawWorkflowState.DrawMode);
            }
        }

        public void CompleteReturn()
        {
            if (CurrentState != CardDrawWorkflowState.ReturningToCity)
            {
#if UNITY_EDITOR
                UnityEngine.Debug.LogWarning(
                    "[CardDrawWorkflowStateMachine] CompleteReturn ignored; current state is "
                        + CurrentState + ".");
#endif
                return;
            }

            Transition(CardDrawWorkflowState.Idle);
        }

        // Centralizes the "CurrentState = X + fire event" pair so every
        // state transition flows through one place. Idempotency guard
        // keeps spurious self-transitions silent on the wire.
        private void Transition(CardDrawWorkflowState next)
        {
            if (CurrentState == next) return;
            CurrentState = next;
            StateChanged?.Invoke(next);
        }
    }
}
