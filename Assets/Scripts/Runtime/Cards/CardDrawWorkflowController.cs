#nullable enable

using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Serialization;

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
        }

        public void OnDrawButtonClicked() =>
            this.HandleDrawClickedAsync().Forget(ex => Debug.LogException(ex, this));

        public void OnReturnButtonClicked() =>
            this.HandleReturnClickedAsync().Forget(ex => Debug.LogException(ex, this));

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
