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
        [Tooltip("Legacy/manual override used as a fallback when the active animator clip length cannot be sampled.")]
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
            yield return null;

            float duration = Mathf.Max(0.01f, GetCurrentClipLength());
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            CompleteOperation();
        }

        private float GetCurrentClipLength()
        {
            if (animator == null)
            {
                return animationDuration;
            }

            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            return state.length > 0f ? state.length : animationDuration;
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
