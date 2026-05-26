using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Runtime.Cards
{
    [DisallowMultipleComponent]
    public sealed class CardDrawWorkflowController : MonoBehaviour, ICardDrawWorkflowCommands
    {
        public void RequestDraw() { OnDrawButtonClicked(); }
        public void RequestReturn() { OnReturnButtonClicked(); }


        [Header("Dependencies")]
        [FormerlySerializedAs("drawGamePresenter")]
        [SerializeField] private MonoBehaviour drawGameActionsSource;
        [FormerlySerializedAs("cameraTransitionService")]
        [SerializeField] private MonoBehaviour cameraTransitionServiceSource;

        [Header("Anchors")]
        [SerializeField] private Transform cardBoardAnchor;
        [SerializeField] private Transform cityViewAnchor;

        [Header("Optional")]
        [SerializeField] private MonoBehaviour drawAnimatorSource;

        private IDrawGameActions drawGameActions;
        private ICameraTransitionService cameraTransitionService;
        private IDrawAnimator drawAnimator;

        private readonly CardDrawWorkflowStateMachine workflowState =
            new CardDrawWorkflowStateMachine();

        private DrawWorkflowExecutor executor;

        private void Awake()
        {
            ResolveDependencies();
            ValidateDependencies();
            executor = new DrawWorkflowExecutor(
                cameraTransitionService,
                drawAnimator,
                drawGameActions,
                workflowState,
                cardBoardAnchor,
                cityViewAnchor,
                this);
        }

        public async void OnDrawButtonClicked()
        {
            try
            {
                DrawWorkflowAction action = workflowState.HandleDrawClicked();
                if (action == DrawWorkflowAction.None)
                {
                    return;
                }

                if (action == DrawWorkflowAction.MoveToBoard)
                {
                    await executor.MoveCameraToBoardAsync();
                    return;
                }

                if (action == DrawWorkflowAction.Draw)
                {
                    await executor.ExecuteDrawAsync();
                }
            }
            catch (System.OperationCanceledException)
            {
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex, this);
                workflowState.ResetToIdle();
            }
        }

        public async void OnReturnButtonClicked()
        {
            try
            {
                if (!workflowState.TryBeginReturn())
                {
                    return;
                }

                await executor.ReturnToCityAsync();
            }
            catch (System.OperationCanceledException)
            {
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex, this);
                workflowState.ResetToIdle();
            }
        }

        private void ResolveDependencies()
        {
            if (drawGameActionsSource != null)
            {
                drawGameActions = drawGameActionsSource as IDrawGameActions;
            }

            if (drawGameActions == null)
            {
                drawGameActions = GetComponent<IDrawGameActions>();
            }

            if (cameraTransitionServiceSource != null)
            {
                cameraTransitionService = cameraTransitionServiceSource as ICameraTransitionService;
            }

            if (cameraTransitionService == null)
            {
                cameraTransitionService = GetComponent<ICameraTransitionService>();
            }

            if (drawAnimatorSource != null)
            {
                drawAnimator = drawAnimatorSource as IDrawAnimator;
            }

            if (drawAnimator == null)
            {
                drawAnimator = GetComponent<IDrawAnimator>();
            }
        }

        private void ValidateDependencies()
        {
            if (drawGameActions == null)
            {
                Debug.LogWarning(
                    "[CardDrawWorkflowController] Draw game actions dependency is not assigned.",
                    this);
            }

            if (cameraTransitionService == null)
            {
                Debug.LogWarning(
                    "[CardDrawWorkflowController] Camera transition service dependency is not assigned.",
                    this);
            }

            if (cardBoardAnchor == null)
            {
                Debug.LogWarning(
                    "[CardDrawWorkflowController] CardBoardAnchor is not assigned.",
                    this);
            }

            if (cityViewAnchor == null)
            {
                Debug.LogWarning(
                    "[CardDrawWorkflowController] CityViewAnchor is not assigned.",
                    this);
            }

            if (drawAnimatorSource != null && drawAnimator == null)
            {
                Debug.LogWarning(
                    "[CardDrawWorkflowController] Draw animator source does not implement IDrawAnimator.",
                    this);
            }
        }
    }
}
