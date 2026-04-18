using System;
using System.Threading.Tasks;
using Game.Domain.Cards;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.Cards
{
    [DisallowMultipleComponent]
    public sealed class CardDrawWorkflowController : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private DrawGamePresenter drawGamePresenter;
        [SerializeField] private CameraTransitionService cameraTransitionService;
        [SerializeField] private Button drawButton;
        [SerializeField] private Button returnButton;
        [SerializeField] private Transform cardBoardAnchor;
        [SerializeField] private Transform cityViewAnchor;
        [SerializeField] private MonoBehaviour drawAnimatorSource;

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
            if (drawGamePresenter == null)
            {
                drawGamePresenter = FindFirstObjectByType<DrawGamePresenter>();
            }

            if (cameraTransitionService == null)
            {
                cameraTransitionService = FindFirstObjectByType<CameraTransitionService>();
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

        private void Start()
        {
            AttachButtonListeners();
            ValidateDependencies();
        }

        private void OnDestroy()
        {
            DetachButtonListeners();
        }

        public async void OnDrawButtonClicked()
        {
            Debug.Log($"[CardDrawWorkflowController] Draw button clicked. State={state}", this);

            if (IsBusy())
            {
                Debug.Log("[CardDrawWorkflowController] Ignoring click because workflow is busy.", this);
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
                return;
            }

            Debug.Log($"[CardDrawWorkflowController] Draw button clicked in state {state}, no action taken.", this);
        }

        public async void OnReturnButtonClicked()
        {
            Debug.Log($"[CardDrawWorkflowController] Return button clicked. State={state}", this);

            if (IsBusy() || state == WorkflowState.Idle)
            {
                return;
            }

            await ReturnToCityAsync();
        }

        private bool IsBusy()
        {
            return state == WorkflowState.MovingToBoard || state == WorkflowState.Drawing || state == WorkflowState.ReturningToCity;
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
            Debug.Log("[CardDrawWorkflowController] Starting camera transition to board.", this);

            try
            {
                await cameraTransitionService.StartTransitionAsync(cardBoardAnchor);
                state = WorkflowState.DrawMode;
                Debug.Log("[CardDrawWorkflowController] Camera arrived at board. State=DrawMode", this);
            }
            catch (Exception exception)
            {
                Debug.LogError("[CardDrawWorkflowController] Failed to move camera to board: " + exception.Message, this);
                state = WorkflowState.Idle;
            }
        }

        private async Task ExecuteDrawAsync()
        {
            if (drawGamePresenter == null)
            {
                Debug.LogWarning("[CardDrawWorkflowController] Missing DrawGamePresenter.", this);
                return;
            }

            if (!drawGamePresenter.CanExecuteDraw())
            {
                drawGamePresenter.ShowStatus("Draw is not ready yet.");
                return;
            }

            state = WorkflowState.Drawing;

            if (drawAnimator != null && drawAnimator.HasAnimation)
            {
                await drawAnimator.PlayDrawAnimationAsync();
            }

            AuthoritativeDrawResult result = await drawGamePresenter.TryDrawAsync();
            if (result == null)
            {
                state = WorkflowState.DrawMode;
                return;
            }

            state = result.IsSuccess ? WorkflowState.DrawMode : WorkflowState.DrawMode;
        }

        private async Task ReturnToCityAsync()
        {
            if (cameraTransitionService == null || cityViewAnchor == null)
            {
                Debug.LogWarning("[CardDrawWorkflowController] Missing camera transition service or city anchor.", this);
                return;
            }

            state = WorkflowState.ReturningToCity;
            Debug.Log("[CardDrawWorkflowController] Returning camera to city.", this);

            try
            {
                await cameraTransitionService.StartTransitionAsync(cityViewAnchor);
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

        private void AttachButtonListeners()
        {
            if (drawButton != null)
            {
                drawButton.onClick.RemoveListener(OnDrawButtonClicked);
                drawButton.onClick.AddListener(OnDrawButtonClicked);
            }
            else
            {
                Debug.LogWarning("[CardDrawWorkflowController] Draw button is not assigned.", this);
            }

            if (returnButton != null)
            {
                returnButton.onClick.RemoveListener(OnReturnButtonClicked);
                returnButton.onClick.AddListener(OnReturnButtonClicked);
            }
        }

        private void DetachButtonListeners()
        {
            if (drawButton != null)
            {
                drawButton.onClick.RemoveListener(OnDrawButtonClicked);
            }

            if (returnButton != null)
            {
                returnButton.onClick.RemoveListener(OnReturnButtonClicked);
            }
        }

        private void ValidateDependencies()
        {
            if (drawGamePresenter == null)
            {
                Debug.LogWarning("[CardDrawWorkflowController] DrawGamePresenter is not assigned.", this);
            }

            if (cameraTransitionService == null)
            {
                Debug.LogWarning("[CardDrawWorkflowController] CameraTransitionService is not assigned.", this);
            }

            if (cardBoardAnchor == null)
            {
                Debug.LogWarning("[CardDrawWorkflowController] CardBoardAnchor is not assigned.", this);
            }

            if (cityViewAnchor == null)
            {
                Debug.LogWarning("[CardDrawWorkflowController] CityViewAnchor is not assigned.", this);
            }
        }
    }
}
