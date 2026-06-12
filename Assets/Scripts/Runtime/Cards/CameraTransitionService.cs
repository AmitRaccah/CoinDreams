#nullable enable

using System;
using System.Collections;
using System.Threading.Tasks;
using Game.Runtime.Cameras;
using UnityEngine;

namespace Game.Runtime.Cards
{
    [DisallowMultipleComponent]
    public sealed class CameraTransitionService
        : InterruptibleAsyncOperationBehaviour,
            ICameraTransitionService
    {
        [SerializeField] private Camera? targetCamera;
        [SerializeField] private float transitionDuration = 1.0f;
        [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        private Camera? cachedCamera;

        public bool IsTransitioning => this.HasActiveOperation;

        public CameraPose CurrentPose
        {
            get
            {
                Camera? camera = this.GetCamera();
                if (camera == null)
                {
                    throw new InvalidOperationException("No camera available for transition.");
                }

                return CameraPose.FromCamera(camera);
            }
        }

        private void Awake() => this.cachedCamera = this.ResolveCamera();

        public Task StartTransitionAsync(Transform destination)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            Camera? camera = this.GetCamera();
            if (camera == null)
            {
                return Task.FromException(new InvalidOperationException("No camera available for transition."));
            }

            return this.StartTransitionAsync(camera, ResolveDestinationPose(destination));
        }

        public Task StartTransitionAsync(CameraPose destination)
        {
            Camera? camera = this.GetCamera();
            if (camera == null)
            {
                return Task.FromException(new InvalidOperationException("No camera available for transition."));
            }

            return this.StartTransitionAsync(camera, destination);
        }

        private Camera? GetCamera()
        {
            if (this.cachedCamera != null)
            {
                return this.cachedCamera;
            }

            this.cachedCamera = this.ResolveCamera();
            return this.cachedCamera;
        }

        private Camera? ResolveCamera() => this.targetCamera != null ? this.targetCamera : Camera.main;

        private Task StartTransitionAsync(Camera camera, CameraPose destination)
        {
            return this.RunOperationAsync(() => this.TransitionCoroutine(camera, destination));
        }

        private static CameraPose ResolveDestinationPose(Transform destination)
        {
            ICameraPoseProvider? poseProvider = destination.GetComponent<ICameraPoseProvider>();
            return poseProvider != null ? poseProvider.GetPose() : CameraPose.FromTransform(destination);
        }

        private IEnumerator TransitionCoroutine(Camera camera, CameraPose destination)
        {
            Transform cameraTransform = camera.transform;
            Vector3 startPosition = cameraTransform.position;
            Quaternion startRotation = cameraTransform.rotation;
            float startOrthographicSize = camera.orthographicSize;
            Vector3 targetPosition = destination.Position;
            Quaternion targetRotation = destination.Rotation;
            float targetOrthographicSize = destination.HasOrthographicSize
                ? Mathf.Max(0.0001f, destination.OrthographicSize)
                : startOrthographicSize;

            float duration = Mathf.Max(0.0001f, this.transitionDuration);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float eased = this.transitionCurve.Evaluate(progress);

                cameraTransform.position = Vector3.Lerp(startPosition, targetPosition, eased);
                cameraTransform.rotation = Quaternion.Slerp(startRotation, targetRotation, eased);
                if (camera.orthographic && destination.HasOrthographicSize)
                {
                    camera.orthographicSize = Mathf.Lerp(startOrthographicSize, targetOrthographicSize, eased);
                }

                yield return null;
            }

            cameraTransform.position = targetPosition;
            cameraTransform.rotation = targetRotation;
            if (camera.orthographic && destination.HasOrthographicSize)
            {
                camera.orthographicSize = targetOrthographicSize;
            }

            this.CompleteOperation();
        }

        protected override string GetDisableCancellationMessage() =>
            "Camera transition was interrupted because the service was disabled.";

        protected override string GetDestroyCancellationMessage() =>
            "Camera transition was interrupted because the service was destroyed.";
    }
}
