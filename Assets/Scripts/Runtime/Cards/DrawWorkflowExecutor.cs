using System;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Domain.Cards;
using Game.Infrastructure.Persistence;
using Game.Runtime.Cameras;
using UnityEngine;

namespace Game.Runtime.Cards
{
    public sealed class DrawWorkflowExecutor
    {
        private readonly ICameraTransitionService cameraTransitionService;
        private readonly IDrawGameActions drawGameActions;
        private readonly IDrawCardPresentation drawCardPresentation;
        private readonly IPlayerSnapshotService snapshotService;
        private readonly CardDrawWorkflowStateMachine workflowState;
        private readonly Transform cardBoardAnchor;
        private readonly Transform cityViewAnchor;
        private readonly ICameraViewModeWriter cameraViewModeWriter;
        private readonly UnityEngine.Object logContext;
        private CameraPose? lastCityPose;

        public DrawWorkflowExecutor(
            ICameraTransitionService cameraTransitionService,
            IDrawGameActions drawGameActions,
            IDrawCardPresentation drawCardPresentation,
            IPlayerSnapshotService snapshotService,
            CardDrawWorkflowStateMachine workflowState,
            Transform cardBoardAnchor,
            Transform cityViewAnchor,
            ICameraViewModeWriter cameraViewModeWriter,
            UnityEngine.Object logContext)
        {
            this.cameraTransitionService = cameraTransitionService;
            this.drawGameActions = drawGameActions;
            this.drawCardPresentation = drawCardPresentation;
            this.snapshotService = snapshotService;
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

            // Open a snapshot-apply deferral window for the entire flow.
            // Both the direct service apply (inside TryDrawAsync) and the
            // Firestore live-sync listener (fires async on commit) target
            // PlayerSnapshotService.OnAuthoritativeSnapshotApplied — the
            // deferral buffers BOTH paths and replays the newest snapshot
            // once we close the window, so the HUD coin counter only
            // moves after the card visual has landed.
            snapshotService?.BeginDeferredApply();
            try
            {
                float drawStartedAt = Time.realtimeSinceStartup;
                float drawLockSeconds = drawCardPresentation.BeginDraw();
                AuthoritativeDrawResult result = await drawGameActions.TryDrawAsync();
                float revealLockSeconds = drawCardPresentation.Present(result);
                await WaitForPresentationLockAsync(drawStartedAt, drawLockSeconds, revealLockSeconds);
                // Fires the steal launcher + result-published signal AFTER
                // the visual lock. Snapshot still buffered — it's flushed
                // in the finally below.
                drawGameActions.ApplyResult(result);
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
                // Close the deferral — applies the buffered snapshot (if any).
                // Wrapped in its own try so a flush failure can't swallow the
                // earlier exception path.
                try
                {
                    snapshotService?.EndDeferredApply();
                }
                catch (Exception flushException)
                {
                    Debug.LogError(
                        "[DrawWorkflowExecutor] Snapshot flush failed: " + flushException.Message,
                        logContext);
                }
                workflowState.CompleteDraw();
            }
        }

        private static async UniTask WaitForPresentationLockAsync(
            float drawStartedAt,
            float drawLockSeconds,
            float revealLockSeconds)
        {
            float elapsed = Time.realtimeSinceStartup - drawStartedAt;
            float remainingDrawSeconds = Mathf.Max(0f, drawLockSeconds - elapsed);
            float waitSeconds = Mathf.Max(remainingDrawSeconds, revealLockSeconds);
            if (waitSeconds <= 0f)
            {
                return;
            }

            await UniTask.Delay(
                TimeSpan.FromSeconds(waitSeconds),
                DelayType.Realtime,
                PlayerLoopTiming.Update);
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
