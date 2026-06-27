using System;
using System.Collections.Generic;
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
        private readonly IReadOnlyList<ICardDrawEffect> effects;
        private readonly CardDrawWorkflowStateMachine workflowState;
        private readonly Transform cardBoardAnchor;
        private readonly Transform cityViewAnchor;
        private readonly ICameraViewModeWriter cameraViewModeWriter;
        private readonly UnityEngine.Object logContext;
        private CameraPose? lastCityPose;

        // Scratch buffer reused across draws — no per-draw List allocation.
        // Capacity 8 covers any reasonable card-type fanout; List<T> grows
        // on demand if a future expansion needs more.
        private readonly List<ICardDrawEffect> activeEffectsBuffer = new List<ICardDrawEffect>(8);

        public DrawWorkflowExecutor(
            ICameraTransitionService cameraTransitionService,
            IDrawGameActions drawGameActions,
            IDrawCardPresentation drawCardPresentation,
            IPlayerSnapshotService snapshotService,
            IReadOnlyList<ICardDrawEffect> effects,
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
            this.effects = effects ?? Array.Empty<ICardDrawEffect>();
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

            // Snapshot deferral wraps the entire flow so HUD coin/energy
            // updates from the live-sync listener (and the direct service
            // apply) are buffered until after the visual + effect commits.
            // Together with the effect Prepare/Apply split this guarantees
            // the player sees no value change until the card lands.
            snapshotService?.BeginDeferredApply();
            try
            {
                // Affordability gate BEFORE any optimistic animation. A draw the
                // player can't afford shows only the failure feedback — never the
                // draw animation — and commits nothing.
                CardDrawContext? rejection = drawGameActions.TryRejectUnaffordableDraw();
                if (rejection.HasValue)
                {
                    CardDrawContext rejected = rejection.Value;
                    float failLockSeconds = drawCardPresentation.Present(rejected.Result, rejected.Multiplier);
                    await WaitForPresentationLockAsync(Time.realtimeSinceStartup, 0f, failLockSeconds);
                    drawGameActions.PublishResult(rejected.Result);
                    return;
                }

                float drawStartedAt = Time.realtimeSinceStartup;
                float drawLockSeconds = drawCardPresentation.BeginDraw();
                CardDrawContext context = await drawGameActions.TryDrawAsync();
                float revealLockSeconds = drawCardPresentation.Present(context.Result, context.Multiplier);

                // Filter active effects without LINQ — index loop, no
                // closures, no iterator allocation. Scratch list reused
                // across draws (Clear keeps the backing array).
                activeEffectsBuffer.Clear();
                int effectCount = effects.Count;
                for (int i = 0; i < effectCount; i++)
                {
                    ICardDrawEffect effect = effects[i];
                    if (effect.ShouldRun(in context))
                    {
                        activeEffectsBuffer.Add(effect);
                    }
                }
                int activeCount = activeEffectsBuffer.Count;

                // Parallel wait set: one Prepare per active effect + the
                // animation lock. Single UniTask[] alloc per draw (size 1
                // when no effects active, N+1 otherwise). Per-draw alloc
                // is fine — draws are user-paced, not per-frame.
                if (activeCount > 0)
                {
                    UniTask[] parallel = new UniTask[activeCount + 1];
                    for (int i = 0; i < activeCount; i++)
                    {
                        parallel[i] = activeEffectsBuffer[i].PrepareAsync(context, default);
                    }
                    parallel[activeCount] = WaitForPresentationLockAsync(
                        drawStartedAt, drawLockSeconds, revealLockSeconds);
                    await UniTask.WhenAll(parallel);
                }
                else
                {
                    await WaitForPresentationLockAsync(
                        drawStartedAt, drawLockSeconds, revealLockSeconds);
                }

                // Commit: HUD publish first (always), then card-type-
                // specific Apply in registration order. Both run AFTER the
                // visual lock so signals fire and the doll appears the
                // instant the card lands.
                drawGameActions.PublishResult(context.Result);
                for (int i = 0; i < activeCount; i++)
                {
                    activeEffectsBuffer[i].Apply(in context);
                }
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
                // Close the snapshot deferral — flushes the latest buffered
                // snapshot to the HUD. Wrapped so a flush failure can't
                // swallow an earlier exception.
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
