using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

namespace Game.Runtime.Cards
{
    [DisallowMultipleComponent]
    public sealed class SimpleDrawAnimator
        : InterruptibleAsyncOperationBehaviour,
            IDrawAnimator
    {
        [SerializeField] private Animator animator;
        [SerializeField] private string drawTrigger = "Draw";
        [SerializeField] private float animationDuration = 0.6f;

        public bool HasAnimation => animator != null && !string.IsNullOrWhiteSpace(drawTrigger) && animationDuration > 0f;

        public Task PlayDrawAnimationAsync()
        {
            if (!HasAnimation)
            {
                return Task.CompletedTask;
            }

            return RunOperationAsync(PlayAnimationCoroutine);
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

            CompleteOperation();
        }

        protected override string GetDisableCancellationMessage()
        {
            return "Draw animation was interrupted because the animator was disabled.";
        }

        protected override string GetDestroyCancellationMessage()
        {
            return "Draw animation was interrupted because the animator was destroyed.";
        }
    }
}
