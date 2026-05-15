using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Game.Runtime.Cards
{
    public sealed class DrawWorkflowExecutor
    {
        private readonly ICameraTransitionService cameraTransitionService;
        private readonly IDrawAnimator drawAnimator;
        private readonly IDrawGameActions drawGameActions;
        private readonly CardDrawWorkflowStateMachine workflowState;
        private readonly Transform cardBoardAnchor;
        private readonly Transform cityViewAnchor;
        private readonly UnityEngine.Object logContext;

        public DrawWorkflowExecutor(
            ICameraTransitionService cameraTransitionService,
            IDrawAnimator drawAnimator,
            IDrawGameActions drawGameActions,
            CardDrawWorkflowStateMachine workflowState,
            Transform cardBoardAnchor,
            Transform cityViewAnchor,
            UnityEngine.Object logContext)
        {
            this.cameraTransitionService = cameraTransitionService;
            this.drawAnimator = drawAnimator;
            this.drawGameActions = drawGameActions;
            this.workflowState = workflowState;
            this.cardBoardAnchor = cardBoardAnchor;
            this.cityViewAnchor = cityViewAnchor;
            this.logContext = logContext;
        }

        public async Task MoveCameraToBoardAsync()
        {
            if (cameraTransitionService == null || cardBoardAnchor == null)
            {
                Debug.LogWarning(
                    "[DrawWorkflowExecutor] Missing camera transition service or board anchor.",
                    logContext);
                workflowState.CompleteMoveToBoard(false);
                return;
            }

            try
            {
                await cameraTransitionService.StartTransitionAsync(cardBoardAnchor);
                workflowState.CompleteMoveToBoard(true);
            }
            catch (OperationCanceledException)
            {
                workflowState.CompleteMoveToBoard(false);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[DrawWorkflowExecutor] Failed to move camera to board: " + exception.Message,
                    logContext);
                workflowState.CompleteMoveToBoard(false);
            }
        }

        public async Task ExecuteDrawAsync()
        {
            if (drawGameActions == null)
            {
                Debug.LogWarning(
                    "[DrawWorkflowExecutor] Missing draw game actions dependency.",
                    logContext);
                workflowState.CompleteDraw();
                return;
            }

            try
            {
                if (drawAnimator != null && drawAnimator.HasAnimation)
                {
                    await drawAnimator.PlayDrawAnimationAsync();
                }

                await drawGameActions.TryDrawAsync();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[DrawWorkflowExecutor] Failed to execute draw: " + exception.Message,
                    logContext);
            }
            finally
            {
                workflowState.CompleteDraw();
            }
        }

        public async Task ReturnToCityAsync()
        {
            if (cameraTransitionService == null || cityViewAnchor == null)
            {
                Debug.LogWarning(
                    "[DrawWorkflowExecutor] Missing camera transition service or city anchor.",
                    logContext);
                workflowState.CompleteReturn();
                return;
            }

            try
            {
                await cameraTransitionService.StartTransitionAsync(cityViewAnchor);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[DrawWorkflowExecutor] Failed to return camera to city: " + exception.Message,
                    logContext);
            }
            finally
            {
                workflowState.CompleteReturn();
            }
        }
    }
}
