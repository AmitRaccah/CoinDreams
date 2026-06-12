#nullable enable

using UnityEngine;

namespace Game.Runtime.Cameras
{
    [DisallowMultipleComponent]
    public sealed class OrbitCameraRig : MonoBehaviour, IOrbitCameraRig
    {
        [Header("References")]
        [SerializeField] private Transform? rigRoot;
        [SerializeField] private UnityEngine.Camera? targetCamera;
        [SerializeField] private Transform? orbitCenter;

        [Header("Yaw Limits")]
        [SerializeField] private float minYawDegrees = -45f;
        [SerializeField] private float maxYawDegrees = 45f;

        [Header("Motion")]
        [SerializeField] private float damping = 18f;

        private Vector3 initialOffset;
        private Quaternion initialRotation;
        private float currentYawDegrees;
        private float targetYawDegrees;
        private bool initialized;

        private void Awake()
        {
            InitializeIfNeeded();
            ApplyImmediate();
        }

        public void AddYawDelta(float deltaDegrees)
        {
            InitializeIfNeeded();
            targetYawDegrees = Mathf.Clamp(targetYawDegrees + deltaDegrees, minYawDegrees, maxYawDegrees);
        }

        public void Tick(float deltaTime)
        {
            InitializeIfNeeded();

            if (rigRoot == null || orbitCenter == null)
            {
                return;
            }

            float t = GetDampedT(deltaTime);
            currentYawDegrees = Mathf.Lerp(currentYawDegrees, targetYawDegrees, t);
            ApplyCurrentYaw();
        }

        public void CancelMotion()
        {
            InitializeIfNeeded();
            targetYawDegrees = currentYawDegrees;
        }

        private void InitializeIfNeeded()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;

            if (targetCamera != null)
            {
                rigRoot = targetCamera.transform;
            }

            if (rigRoot == null)
            {
                rigRoot = transform;
            }

            if (rigRoot == null || orbitCenter == null)
            {
                Debug.LogWarning("[OrbitCameraRig] Missing rig root or orbit center.", this);
                return;
            }

            initialOffset = rigRoot.position - orbitCenter.position;
            initialRotation = rigRoot.rotation;
            currentYawDegrees = Mathf.Clamp(0f, minYawDegrees, maxYawDegrees);
            targetYawDegrees = currentYawDegrees;
        }

        private void ApplyImmediate()
        {
            currentYawDegrees = targetYawDegrees;
            ApplyCurrentYaw();
        }

        private void ApplyCurrentYaw()
        {
            if (rigRoot == null || orbitCenter == null)
            {
                return;
            }

            Quaternion orbitRotation = Quaternion.AngleAxis(currentYawDegrees, Vector3.up);
            rigRoot.position = orbitCenter.position + orbitRotation * initialOffset;
            rigRoot.rotation = orbitRotation * initialRotation;
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
