using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

namespace Game.Runtime.Cards
{
    [DisallowMultipleComponent]
    public sealed class CameraTransitionService
        : InterruptibleAsyncOperationBehaviour,
            ICameraTransitionService
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private float transitionDuration = 1.0f;
        [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        public bool IsTransitioning
        {
            get { return HasActiveOperation; }
        }

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

            return RunOperationAsync(() => TransitionCoroutine(destination));
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

            CompleteOperation();
        }

        protected override string GetDisableCancellationMessage()
        {
            return "Camera transition was interrupted because the service was disabled.";
        }

        protected override string GetDestroyCancellationMessage()
        {
            return "Camera transition was interrupted because the service was destroyed.";
        }
    }
}
