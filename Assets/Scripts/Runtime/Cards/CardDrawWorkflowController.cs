using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Runtime.Cards
{
    [DisallowMultipleComponent]
    public sealed class CardDrawWorkflowController : MonoBehaviour
    {
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

        private void Awake()
        {
            ResolveDependencies();
        }

        public async void OnDrawButtonClicked()
        {
            DrawWorkflowAction action = workflowState.HandleDrawClicked();
            if (action == DrawWorkflowAction.None)
            {
                return;
            }

            if (action == DrawWorkflowAction.MoveToBoard)
            {
                await MoveCameraToBoardAsync();
                return;
            }

            if (action == DrawWorkflowAction.Draw)
            {
                await ExecuteDrawAsync();
            }
        }

        public async void OnReturnButtonClicked()
        {
            if (!workflowState.TryBeginReturn())
            {
                return;
            }

            await ReturnToCityAsync();
        }

        private async Task MoveCameraToBoardAsync()
        {
            if (cameraTransitionService == null || cardBoardAnchor == null)
            {
                Debug.LogWarning("[CardDrawWorkflowController] Missing camera transition service or board anchor.", this);
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
                Debug.LogError("[CardDrawWorkflowController] Failed to move camera to board: " + exception.Message, this);
                workflowState.CompleteMoveToBoard(false);
            }
        }

        private async Task ExecuteDrawAsync()
        {
            if (drawGameActions == null)
            {
                Debug.LogWarning("[CardDrawWorkflowController] Missing draw game actions dependency.", this);
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
                // Ignore cancellation during teardown/disable.
            }
            catch (Exception exception)
            {
                Debug.LogError("[CardDrawWorkflowController] Failed to execute draw: " + exception.Message, this);
            }
            finally
            {
                workflowState.CompleteDraw();
            }
        }

        private async Task ReturnToCityAsync()
        {
            if (cameraTransitionService == null || cityViewAnchor == null)
            {
                Debug.LogWarning("[CardDrawWorkflowController] Missing camera transition service or city anchor.", this);
                workflowState.CompleteReturn();
                return;
            }

            try
            {
                await cameraTransitionService.StartTransitionAsync(cityViewAnchor);
            }
            catch (OperationCanceledException)
            {
                // Ignore cancellation during teardown/disable.
            }
            catch (Exception exception)
            {
                Debug.LogError("[CardDrawWorkflowController] Failed to return camera to city: " + exception.Message, this);
            }
            finally
            {
                workflowState.CompleteReturn();
            }
        }

        private void ValidateDependencies()
        {
            if (drawGameActions == null)
            {
                Debug.LogWarning("[CardDrawWorkflowController] Draw game actions dependency is not assigned.", this);
            }

            if (cameraTransitionService == null)
            {
                Debug.LogWarning("[CardDrawWorkflowController] Camera transition service dependency is not assigned.", this);
            }

            if (cardBoardAnchor == null)
            {
                Debug.LogWarning("[CardDrawWorkflowController] CardBoardAnchor is not assigned.", this);
            }

            if (cityViewAnchor == null)
            {
                Debug.LogWarning("[CardDrawWorkflowController] CityViewAnchor is not assigned.", this);
            }

            if (drawAnimatorSource != null && drawAnimator == null)
            {
                Debug.LogWarning("[CardDrawWorkflowController] Draw animator source does not implement IDrawAnimator.", this);
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

            ValidateDependencies();
        }
    }
}
