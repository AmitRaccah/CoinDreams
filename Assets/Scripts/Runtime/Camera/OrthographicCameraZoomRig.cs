#nullable enable

using UnityEngine;

namespace Game.Runtime.Cameras
{
    [DisallowMultipleComponent]
    public sealed class OrthographicCameraZoomRig : MonoBehaviour, ICameraZoomRig
    {
        [Header("References")]
        [SerializeField] private UnityEngine.Camera? targetCamera;

        [Header("Zoom Limits")]
        [SerializeField] private float minOrthographicSize = 1.6f;
        [SerializeField] private float maxOrthographicSize = 3.6f;

        [Header("Motion")]
        [SerializeField] private float damping = 18f;

        private float currentSize;
        private float targetSize;
        private bool initialized;

        private void Awake()
        {
            InitializeIfNeeded();
        }

        public void AddZoomDelta(float deltaSize)
        {
            InitializeIfNeeded();
            targetSize = Mathf.Clamp(targetSize + deltaSize, minOrthographicSize, maxOrthographicSize);
        }

        public void Tick(float deltaTime)
        {
            InitializeIfNeeded();

            UnityEngine.Camera? camera = targetCamera;
            if (camera == null)
            {
                return;
            }

            if (!camera.orthographic)
            {
                return;
            }

            float t = GetDampedT(deltaTime);
            currentSize = Mathf.Lerp(currentSize, targetSize, t);
            camera.orthographicSize = currentSize;
        }

        public void CancelMotion()
        {
            InitializeIfNeeded();
            targetSize = currentSize;
        }

        private void InitializeIfNeeded()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;

            if (targetCamera == null)
            {
                targetCamera = UnityEngine.Camera.main;
            }

            UnityEngine.Camera? camera = targetCamera;
            if (camera == null)
            {
                Debug.LogWarning("[OrthographicCameraZoomRig] Missing target camera.", this);
                return;
            }

            currentSize = Mathf.Clamp(camera.orthographicSize, minOrthographicSize, maxOrthographicSize);
            targetSize = currentSize;
            camera.orthographicSize = currentSize;
        }

        private float GetDampedT(float deltaTime)
        {
            if (damping <= 0f)
            {
                return 1f;
            }

            return 1f - Mathf.Exp(-damping * Mathf.Max(0f, deltaTime));
        }
    }
}
