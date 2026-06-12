using System;
using System.Threading.Tasks;
using Game.Runtime.Cameras;
using UnityEngine;

namespace Game.Runtime.Cards
{
    public sealed class DrawWorkflowExecutor
    {
        private readonly ICameraTransitionService cameraTransitionService;
        private readonly IDrawGameActions drawGameActions;
        private readonly CardDrawWorkflowStateMachine workflowState;
        private readonly Transform cardBoardAnchor;
        private readonly Transform cityViewAnchor;
        private readonly ICameraViewModeWriter cameraViewModeWriter;
        private readonly UnityEngine.Object logContext;
        private CameraPose? lastCityPose;

        public DrawWorkflowExecutor(
            ICameraTransitionService cameraTransitionService,
            IDrawGameActions drawGameActions,
            CardDrawWorkflowStateMachine workflowState,
            Transform cardBoardAnchor,
            Transform cityViewAnchor,
            ICameraViewModeWriter cameraViewModeWriter,
            UnityEngine.Object logContext)
        {
            this.cameraTransitionService = cameraTransitionService;
            this.drawGameActions = drawGameActions;
            this.workflowState = workflowState;
            this.cardBoardAnchor = cardBoardAnchor;
            this.cityViewAnchor = cityViewAnchor;
            this.cameraViewModeWriter = cameraViewModeWriter;
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
                cameraViewModeWriter?.SetMode(CameraViewMode.City);
                return;
            }

            try
            {
                lastCityPose = cameraTransitionService.CurrentPose;
                cameraViewModeWriter?.SetMode(CameraViewMode.Transitioning);
                await cameraTransitionService.StartTransitionAsync(cardBoardAnchor);
                workflowState.CompleteMoveToBoard(true);
                cameraViewModeWriter?.SetMode(CameraViewMode.Board);
            }
            catch (OperationCanceledException)
            {
                workflowState.CompleteMoveToBoard(false);
                cameraViewModeWriter?.SetMode(CameraViewMode.City);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[DrawWorkflowExecutor] Failed to move camera to board: " + exception.Message,
                    logContext);
                workflowState.CompleteMoveToBoard(false);
                cameraViewModeWriter?.SetMode(CameraViewMode.City);
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
                cameraViewModeWriter?.SetMode(CameraViewMode.City);
                return;
            }

            bool returnedToCity = false;
            try
            {
                cameraViewModeWriter?.SetMode(CameraViewMode.Transitioning);
                if (lastCityPose.HasValue)
                {
                    await cameraTransitionService.StartTransitionAsync(lastCityPose.Value);
                }
                else
                {
                    await cameraTransitionService.StartTransitionAsync(cityViewAnchor);
                }

                returnedToCity = true;
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
                if (returnedToCity)
                {
                    lastCityPose = null;
                }

                cameraViewModeWriter?.SetMode(returnedToCity ? CameraViewMode.City : CameraViewMode.Board);
            }
        }
    }
}
