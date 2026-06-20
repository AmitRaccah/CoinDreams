#nullable enable

using System;
using Cysharp.Threading.Tasks;
using Game.Composition.Signals;
using Game.Runtime.Cameras;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace Game.Runtime.Cards
{
    [DisallowMultipleComponent]
    public sealed class CardDrawWorkflowController : MonoBehaviour, IDrawWorkflowStateReader
    {
        // Forwards to the internal state machine so consumers that depend
        // on IDrawWorkflowStateReader (e.g. DrawWorkflowTagsPublisher) get
        // notified the moment the workflow advances, without taking a
        // dependency on this MonoBehaviour or the state-machine type.
        public CardDrawWorkflowState CurrentState => workflowState.CurrentState;
        public event Action<CardDrawWorkflowState>? StateChanged
        {
            add { workflowState.StateChanged += value; }
            remove { workflowState.StateChanged -= value; }
        }

        [Header("Dependencies")]
        [SerializeField] private MonoBehaviour? cameraTransitionServiceSource;

        [Header("Anchors")]
        [SerializeField] private Transform? cardBoardAnchor;
        [SerializeField] private Transform? cityViewAnchor;

        private IDrawGameActions? drawGameActions;
        private ICameraTransitionService? cameraTransitionService;

        [Inject] private ISubscriber<DrawRequestedSignal>? drawSubscriber;
        [Inject] private ISubscriber<ReturnRequestedSignal>? returnSubscriber;
        [Inject] private ICameraViewModeWriter? cameraViewModeWriter;

        private IDisposable? drawSubscription;
        private IDisposable? returnSubscription;

        private readonly CardDrawWorkflowStateMachine workflowState = new CardDrawWorkflowStateMachine();

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
            this.executor = new DrawWorkflowExecutor(
                this.cameraTransitionService,
                this.drawGameActions,
                this.workflowState,
                this.cardBoardAnchor,
                this.cityViewAnchor,
                this.cameraViewModeWriter,
                this);

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
            DrawWorkflowAction action = this.workflowState.HandleDrawClicked();
            if (action == DrawWorkflowAction.None)
            {
                return;
            }

            if (this.executor == null)
            {
                return;
            }

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
            if (!this.workflowState.TryBeginReturn())
            {
                return;
            }

            if (this.executor == null)
            {
                return;
            }

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
