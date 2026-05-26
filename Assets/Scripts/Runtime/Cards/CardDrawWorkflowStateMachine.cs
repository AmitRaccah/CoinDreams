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

    public sealed class CardDrawWorkflowStateMachine
    {
        public CardDrawWorkflowState CurrentState { get; private set; } = CardDrawWorkflowState.Idle;

        public bool IsBusy
        {
            get
            {
                return CurrentState == CardDrawWorkflowState.MovingToBoard
                    || CurrentState == CardDrawWorkflowState.Drawing
                    || CurrentState == CardDrawWorkflowState.ReturningToCity;
            }
        }

        public DrawWorkflowAction HandleDrawClicked()
        {
            if (CurrentState == CardDrawWorkflowState.Idle)
            {
                CurrentState = CardDrawWorkflowState.MovingToBoard;
                return DrawWorkflowAction.MoveToBoard;
            }

            if (CurrentState == CardDrawWorkflowState.DrawMode)
            {
                CurrentState = CardDrawWorkflowState.Drawing;
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

            CurrentState = CardDrawWorkflowState.ReturningToCity;
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

            CurrentState = succeeded
                ? CardDrawWorkflowState.DrawMode
                : CardDrawWorkflowState.Idle;
        }

        public void CompleteDraw()
        {
            if (CurrentState == CardDrawWorkflowState.Drawing)
            {
                CurrentState = CardDrawWorkflowState.DrawMode;
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

            CurrentState = CardDrawWorkflowState.Idle;
        }

        /// <summary>
        /// Forces the state machine back to <see cref="CardDrawWorkflowState.Idle"/> without
        /// validating the current state. Intended only for exception recovery paths where the
        /// workflow has been aborted and the normal Complete* transitions cannot run.
        /// </summary>
        public void ResetToIdle()
        {
            CurrentState = CardDrawWorkflowState.Idle;
        }
    }
}
