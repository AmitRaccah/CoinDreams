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
        private WorkflowState state = WorkflowState.Idle;

        private enum WorkflowState
        {
            Idle,
            MovingToBoard,
            DrawMode,
            Drawing,
            ReturningToCity
        }

        private void Awake()
        {
            ResolveDependencies();
        }

        public async void OnDrawButtonClicked()
        {
            if (IsBusy())
            {
                return;
            }

            if (state == WorkflowState.Idle)
            {
                await MoveCameraToBoardAsync();
                return;
            }

            if (state == WorkflowState.DrawMode)
            {
                await ExecuteDrawAsync();
            }
        }

        public async void OnReturnButtonClicked()
        {
            if (IsBusy() || state == WorkflowState.Idle)
            {
                return;
            }

            await ReturnToCityAsync();
        }

        private bool IsBusy()
        {
            return state == WorkflowState.MovingToBoard
                || state == WorkflowState.Drawing
                || state == WorkflowState.ReturningToCity;
        }

        private async Task MoveCameraToBoardAsync()
        {
            if (cameraTransitionService == null || cardBoardAnchor == null)
            {
                Debug.LogWarning("[CardDrawWorkflowController] Missing camera transition service or board anchor.", this);
                state = WorkflowState.Idle;
                return;
            }

            state = WorkflowState.MovingToBoard;

            try
            {
                await cameraTransitionService.StartTransitionAsync(cardBoardAnchor);
                state = WorkflowState.DrawMode;
            }
            catch (OperationCanceledException)
            {
                state = WorkflowState.Idle;
            }
            catch (Exception exception)
            {
                Debug.LogError("[CardDrawWorkflowController] Failed to move camera to board: " + exception.Message, this);
                state = WorkflowState.Idle;
            }
        }

        private async Task ExecuteDrawAsync()
        {
            if (drawGameActions == null)
            {
                Debug.LogWarning("[CardDrawWorkflowController] Missing draw game actions dependency.", this);
                return;
            }

            state = WorkflowState.Drawing;
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
                state = WorkflowState.DrawMode;
            }
        }

        private async Task ReturnToCityAsync()
        {
            if (cameraTransitionService == null || cityViewAnchor == null)
            {
                Debug.LogWarning("[CardDrawWorkflowController] Missing camera transition service or city anchor.", this);
                return;
            }

            state = WorkflowState.ReturningToCity;

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
                state = WorkflowState.Idle;
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
