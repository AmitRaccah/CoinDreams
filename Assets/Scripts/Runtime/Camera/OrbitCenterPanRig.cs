#nullable enable

using UnityEngine;

namespace Game.Runtime.Cameras
{
    [DisallowMultipleComponent]
    public sealed class OrbitCenterPanRig : MonoBehaviour, IPanRig
    {
        [Header("References")]
        [SerializeField] private Transform? orbitCenter;

        [Header("Pan Limits (World Units)")]
        [SerializeField] private float minPanWorldUnits = -10f;
        [SerializeField] private float maxPanWorldUnits = 10f;

        [Header("Motion")]
        [SerializeField] private float damping = 18f;

        private Vector3 initialOrbitCenterPosition;
        private float currentPanUnits;
        private float targetPanUnits;
        private bool initialized;

        private void Awake()
        {
            InitializeIfNeeded();
            ApplyImmediate();
        }

        public void AddPanDelta(float deltaWorldUnits)
        {
            InitializeIfNeeded();
            targetPanUnits = Mathf.Clamp(
                targetPanUnits + deltaWorldUnits,
                minPanWorldUnits,
                maxPanWorldUnits);
        }

        public void Tick(float deltaTime)
        {
            InitializeIfNeeded();

            if (orbitCenter == null)
            {
                return;
            }

            float t = GetDampedT(deltaTime);
            currentPanUnits = Mathf.Lerp(currentPanUnits, targetPanUnits, t);
            ApplyCurrentPan();
        }

        public void CancelMotion()
        {
            InitializeIfNeeded();
            targetPanUnits = currentPanUnits;
        }

        private void InitializeIfNeeded()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;

            if (orbitCenter == null)
            {
                Debug.LogWarning("[OrbitCenterPanRig] Missing orbit center.", this);
                return;
            }

            initialOrbitCenterPosition = orbitCenter.position;
            currentPanUnits = 0f;
            targetPanUnits = 0f;
        }

        private void ApplyImmediate()
        {
            currentPanUnits = targetPanUnits;
            ApplyCurrentPan();
        }

        private void ApplyCurrentPan()
        {
            if (orbitCenter == null)
            {
                return;
            }

            orbitCenter.position = initialOrbitCenterPosition
                + Vector3.right * currentPanUnits;
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
