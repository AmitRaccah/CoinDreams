#nullable enable

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Signals;
using Game.Infrastructure.Persistence;
using Game.Runtime.Cameras;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace Game.Runtime.Cards
{
    // Thin controller — owns scene-side config (anchors + same-GameObject
    // services) and routes click signals to the workflow. The state itself
    // lives in CardDrawWorkflowStateMachine, registered as a separate
    // service in GameplayLifetimeScope and exposed as IDrawWorkflowStateReader
    // so UI bridges (DrawWorkflowFeelTrigger) subscribe to transitions
    // without depending on this MonoBehaviour or on the concrete state
    // machine.
    //
    // SRP: routing input → workflow + holding scene anchors. State ownership
    // and event publishing live in the state machine, visual reaction lives
    // in the Feel trigger — each layer one responsibility.
    [DisallowMultipleComponent]
    public sealed class CardDrawWorkflowController : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private MonoBehaviour? cameraTransitionServiceSource;

        [Header("Anchors")]
        [SerializeField] private Transform? cardBoardAnchor;
        [SerializeField] private Transform? cityViewAnchor;

        private IDrawGameActions? drawGameActions;
        private ICameraTransitionService? cameraTransitionService;

        [Inject] private CardDrawWorkflowStateMachine? workflowState;
        [Inject] private ISubscriber<DrawRequestedSignal>? drawSubscriber;
        [Inject] private ISubscriber<ReturnRequestedSignal>? returnSubscriber;
        [Inject] private ICameraViewModeWriter? cameraViewModeWriter;
        [Inject] private ICameraCityPoseMemory? cityPoseMemory;
        [Inject] private IDrawCardPresentation? drawCardPresentation;
        [Inject] private IPlayerSnapshotService? snapshotService;
        [Inject] private IReadOnlyList<ICardDrawEffect>? cardDrawEffects;

        private IDisposable? drawSubscription;
        private IDisposable? returnSubscription;

        private DrawWorkflowExecutor? executor;

        private readonly Action<Exception> logUnlessCanceled;

        public CardDrawWorkflowController()
        {
            this.logUnlessCanceled = ex =>
            {
                if (ex is OperationCanceledException) return;
                Debug.LogException(ex, this);
            };
        }

        private void Awake()
        {
            this.ResolveDependencies();
            this.ValidateDependencies();
            if (this.workflowState != null)
            {
                this.executor = new DrawWorkflowExecutor(
                    this.cameraTransitionService,
                    this.drawGameActions,
                    this.drawCardPresentation ?? new NullDrawCardPresentation(),
                    this.snapshotService!,
                    this.cardDrawEffects ?? Array.Empty<ICardDrawEffect>(),
                    this.workflowState,
                    this.cardBoardAnchor,
                    this.cityViewAnchor,
                    this.cameraViewModeWriter,
                    this.cityPoseMemory,
                    this);
            }

            if (drawSubscriber != null)
            {
                this.drawSubscription = drawSubscriber.Subscribe(this.OnDrawSignal);
            }
            if (returnSubscriber != null)
            {
                this.returnSubscription = returnSubscriber.Subscribe(this.OnReturnSignal);
            }
        }

        private void OnDestroy()
        {
            this.drawSubscription?.Dispose();
            this.returnSubscription?.Dispose();
        }

        private void OnDrawSignal(DrawRequestedSignal _) => this.RequestDraw();

        private void OnReturnSignal(ReturnRequestedSignal _) => this.RequestReturn();

        public void RequestDraw() => this.HandleDrawClickedAsync().Forget(this.logUnlessCanceled);

        public void RequestReturn() => this.HandleReturnClickedAsync().Forget(this.logUnlessCanceled);

        // Legacy aliases kept for Unity Button OnClick wiring in existing scene assets.
        public void OnDrawButtonClicked() => this.RequestDraw();
        public void OnReturnButtonClicked() => this.RequestReturn();

        private async UniTask HandleDrawClickedAsync()
        {
            if (this.workflowState == null || this.executor == null) return;

            DrawWorkflowAction action = this.workflowState.HandleDrawClicked();
            if (action == DrawWorkflowAction.None) return;

            if (action == DrawWorkflowAction.MoveToBoard)
            {
                await this.executor.MoveCameraToBoardAsync();
                return;
            }

            if (action == DrawWorkflowAction.Draw)
            {
                await this.executor.ExecuteDrawAsync();
            }
        }

        private async UniTask HandleReturnClickedAsync()
        {
            if (this.workflowState == null || this.executor == null) return;
            if (!this.workflowState.TryBeginReturn()) return;
            await this.executor.ReturnToCityAsync();
        }

        private void ResolveDependencies()
        {
            this.drawGameActions = this.GetComponent<IDrawGameActions>();

            if (this.cameraTransitionServiceSource != null)
            {
                this.cameraTransitionService = this.cameraTransitionServiceSource as ICameraTransitionService;
            }

            if (this.cameraTransitionService == null)
            {
                this.cameraTransitionService = this.GetComponent<ICameraTransitionService>();
            }
        }

        private void ValidateDependencies()
        {
            if (this.drawGameActions == null)
            {
                Debug.LogWarning(
                    "[CardDrawWorkflowController] Draw game actions dependency is not assigned.",
                    this);
            }

            if (this.cameraTransitionService == null)
            {
                Debug.LogWarning(
                    "[CardDrawWorkflowController] Camera transition service dependency is not assigned.",
                    this);
            }

            if (this.cardBoardAnchor == null)
            {
                Debug.LogWarning(
                    "[CardDrawWorkflowController] CardBoardAnchor is not assigned.",
                    this);
            }

            if (this.cityViewAnchor == null)
            {
                Debug.LogWarning(
                    "[CardDrawWorkflowController] CityViewAnchor is not assigned.",
                    this);
            }
        }
    }
}
