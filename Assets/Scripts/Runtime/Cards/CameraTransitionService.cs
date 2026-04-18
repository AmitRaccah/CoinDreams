using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

namespace Game.Runtime.Cards
{
    [DisallowMultipleComponent]
    public sealed class CameraTransitionService : MonoBehaviour, ICameraTransitionService
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private float transitionDuration = 1.0f;
        [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        private Coroutine activeTransition;
        private TaskCompletionSource<bool> activeCompletionSource;

        public bool IsTransitioning => activeTransition != null;

        private void Awake()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }
        }

        public Task StartTransitionAsync(Transform destination)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (targetCamera == null)
            {
                return Task.FromException(new InvalidOperationException("No camera available for transition."));
            }

            if (activeTransition != null)
            {
                return activeCompletionSource != null ? activeCompletionSource.Task : Task.CompletedTask;
            }

            activeCompletionSource = new TaskCompletionSource<bool>();
            activeTransition = StartCoroutine(TransitionCoroutine(destination));
            return activeCompletionSource.Task;
        }

        private IEnumerator TransitionCoroutine(Transform destination)
        {
            Transform cameraTransform = targetCamera.transform;
            Vector3 startPosition = cameraTransform.position;
            Quaternion startRotation = cameraTransform.rotation;
            Vector3 targetPosition = destination.position;
            Quaternion targetRotation = destination.rotation;

            float duration = Mathf.Max(0.0001f, transitionDuration);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float eased = transitionCurve.Evaluate(progress);

                cameraTransform.position = Vector3.Lerp(startPosition, targetPosition, eased);
                cameraTransform.rotation = Quaternion.Slerp(startRotation, targetRotation, eased);

                yield return null;
            }

            cameraTransform.position = targetPosition;
            cameraTransform.rotation = targetRotation;

            activeTransition = null;
            activeCompletionSource?.TrySetResult(true);
            activeCompletionSource = null;
        }
    }
}
