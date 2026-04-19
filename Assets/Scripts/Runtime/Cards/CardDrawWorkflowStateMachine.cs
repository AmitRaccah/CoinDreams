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
            CurrentState = CardDrawWorkflowState.Idle;
        }

        public void ResetToIdle()
        {
            CurrentState = CardDrawWorkflowState.Idle;
        }
    }
}
