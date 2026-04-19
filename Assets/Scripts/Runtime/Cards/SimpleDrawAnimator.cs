using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

namespace Game.Runtime.Cards
{
    [DisallowMultipleComponent]
    public sealed class SimpleDrawAnimator : MonoBehaviour, IDrawAnimator
    {
        [SerializeField] private Animator animator;
        [SerializeField] private string drawTrigger = "Draw";
        [SerializeField] private float animationDuration = 0.6f;

        private Coroutine animationCoroutine;
        private TaskCompletionSource<bool> animationCompletionSource;

        public bool HasAnimation => animator != null && !string.IsNullOrWhiteSpace(drawTrigger) && animationDuration > 0f;

        public Task PlayDrawAnimationAsync()
        {
            if (!HasAnimation)
            {
                return Task.CompletedTask;
            }

            if (animationCoroutine != null)
            {
                return animationCompletionSource != null ? animationCompletionSource.Task : Task.CompletedTask;
            }

            animationCompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            animationCoroutine = StartCoroutine(PlayAnimationCoroutine());
            return animationCompletionSource.Task;
        }

        private void OnDisable()
        {
            CancelAnimation("Draw animation was interrupted because the animator was disabled.");
        }

        private void OnDestroy()
        {
            CancelAnimation("Draw animation was interrupted because the animator was destroyed.");
        }

        private IEnumerator PlayAnimationCoroutine()
        {
            animator.ResetTrigger(drawTrigger);
            animator.SetTrigger(drawTrigger);

            float elapsed = 0f;
            float duration = Mathf.Max(0.01f, animationDuration);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            CompleteAnimation();
        }

        private void CompleteAnimation()
        {
            animationCoroutine = null;

            TaskCompletionSource<bool> completionSource = animationCompletionSource;
            animationCompletionSource = null;

            completionSource?.TrySetResult(true);
        }

        private void CancelAnimation(string message)
        {
            if (animationCoroutine != null)
            {
                StopCoroutine(animationCoroutine);
                animationCoroutine = null;
            }

            TaskCompletionSource<bool> completionSource = animationCompletionSource;
            animationCompletionSource = null;
            if (completionSource == null)
            {
                return;
            }

            completionSource.TrySetException(new OperationCanceledException(message));
        }
    }
}
