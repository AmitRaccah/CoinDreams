#nullable enable

using System;
using Cysharp.Threading.Tasks;
using Game.Composition.Signals;
using MessagePipe;
using UnityEngine;
using UnityEngine.Serialization;
using VContainer;

namespace Game.Runtime.Cards
{
    [DisallowMultipleComponent]
    public sealed class CardDrawWorkflowController : MonoBehaviour
    {
        [Header("Dependencies")]
        [FormerlySerializedAs("drawGamePresenter")]
        [SerializeField] private MonoBehaviour? drawGameActionsSource;
        [FormerlySerializedAs("cameraTransitionService")]
        [SerializeField] private MonoBehaviour? cameraTransitionServiceSource;

        [Header("Anchors")]
        [SerializeField] private Transform? cardBoardAnchor;
        [SerializeField] private Transform? cityViewAnchor;

        [Header("Optional")]
        [SerializeField] private MonoBehaviour? drawAnimatorSource;

        private IDrawGameActions? drawGameActions;
        private ICameraTransitionService? cameraTransitionService;
        private IDrawAnimator? drawAnimator;

        [Inject] private ISubscriber<DrawRequestedSignal>? drawSubscriber;
        [Inject] private ISubscriber<ReturnRequestedSignal>? returnSubscriber;

        private IDisposable? drawSubscription;
        private IDisposable? returnSubscription;

        private readonly CardDrawWorkflowStateMachine workflowState = new CardDrawWorkflowStateMachine();

        private DrawWorkflowExecutor? executor;

        private void Awake()
        {
            this.ResolveDependencies();
            this.ValidateDependencies();
            this.executor = new DrawWorkflowExecutor(
                this.cameraTransitionService,
                this.drawAnimator,
                this.drawGameActions,
                this.workflowState,
                this.cardBoardAnchor,
                this.cityViewAnchor,
                this);

            Debug.Log($"[DIAG] CardDrawWorkflowController.Awake — drawSubscriber={(drawSubscriber == null ? "NULL" : "ok")}, returnSubscriber={(returnSubscriber == null ? "NULL" : "ok")}, executor={(executor == null ? "NULL" : "ok")}", this);
            if (drawSubscriber != null)
            {
                this.drawSubscription = drawSubscriber.Subscribe(_ =>
                {
                    Debug.Log("[DIAG] CardDrawWorkflowController received DrawRequestedSignal", this);
                    this.RequestDraw();
                });
            }
            if (returnSubscriber != null)
            {
                this.returnSubscription = returnSubscriber.Subscribe(_ =>
                {
                    Debug.Log("[DIAG] CardDrawWorkflowController received ReturnRequestedSignal", this);
                    this.RequestReturn();
                });
            }
        }

        private void OnDestroy()
        {
            this.drawSubscription?.Dispose();
            this.returnSubscription?.Dispose();
        }

        public void RequestDraw() =>
            this.HandleDrawClickedAsync().Forget(ex => Debug.LogException(ex, this));

        public void RequestReturn() =>
            this.HandleReturnClickedAsync().Forget(ex => Debug.LogException(ex, this));

        // Legacy aliases kept for Unity Button OnClick wiring in existing scene assets.
        public void OnDrawButtonClicked() => this.RequestDraw();
        public void OnReturnButtonClicked() => this.RequestReturn();

        private async UniTask HandleDrawClickedAsync()
        {
            DrawWorkflowAction action = this.workflowState.HandleDrawClicked();
            Debug.Log($"[DIAG] HandleDrawClickedAsync — state action={action}, executor={(this.executor == null ? "NULL" : "ok")}", this);
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
            if (this.drawGameActionsSource != null)
            {
                this.drawGameActions = this.drawGameActionsSource as IDrawGameActions;
            }

            if (this.drawGameActions == null)
            {
                this.drawGameActions = this.GetComponent<IDrawGameActions>();
            }

            if (this.cameraTransitionServiceSource != null)
            {
                this.cameraTransitionService = this.cameraTransitionServiceSource as ICameraTransitionService;
            }

            if (this.cameraTransitionService == null)
            {
                this.cameraTransitionService = this.GetComponent<ICameraTransitionService>();
            }

            if (this.drawAnimatorSource != null)
            {
                this.drawAnimator = this.drawAnimatorSource as IDrawAnimator;
            }

            if (this.drawAnimator == null)
            {
                this.drawAnimator = this.GetComponent<IDrawAnimator>();
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

            if (this.drawAnimatorSource != null && this.drawAnimator == null)
            {
                Debug.LogWarning(
                    "[CardDrawWorkflowController] Draw animator source does not implement IDrawAnimator.",
                    this);
            }
        }
    }
}
