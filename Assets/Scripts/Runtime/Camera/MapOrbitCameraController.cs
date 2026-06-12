#nullable enable

using UnityEngine;
using VContainer;

namespace Game.Runtime.Cameras
{
    [DisallowMultipleComponent]
    public sealed class MapOrbitCameraController : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] private MonoBehaviour? inputSource;
        [SerializeField] private MonoBehaviour? orbitRigSource;
        [SerializeField] private MonoBehaviour? zoomRigSource;

        [Header("Sensitivity")]
        [SerializeField] private float horizontalDegreesPerPixel = 0.08f;
        [SerializeField] private float verticalZoomSizePerPixel = 0.004f;

        [Header("Axes")]
        [SerializeField] private bool invertHorizontal;
        [SerializeField] private bool invertVerticalZoom;

        [Header("Filtering")]
        [SerializeField] private float deadZonePixels = 0.01f;

        [Inject] private ICameraViewModeReader? viewModeReader;

        private ICameraOrbitInputSource? resolvedInputSource;
        private IOrbitCameraRig? orbitRig;
        private ICameraZoomRig? zoomRig;
        private bool referencesResolved;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();

            if (viewModeReader != null)
            {
                viewModeReader.ModeChanged += HandleModeChanged;
            }
        }

        private void OnDisable()
        {
            if (viewModeReader != null)
            {
                viewModeReader.ModeChanged -= HandleModeChanged;
            }

            resolvedInputSource?.CancelGesture();
            orbitRig?.CancelMotion();
            zoomRig?.CancelMotion();
        }

        private void Update()
        {
            ResolveReferences();

            if (viewModeReader != null && !viewModeReader.IsCityView)
            {
                return;
            }

            OrbitCameraInputFrame inputFrame = resolvedInputSource != null
                ? resolvedInputSource.ReadFrame()
                : OrbitCameraInputFrame.None;

            if (inputFrame.HasInput)
            {
                ApplyInput(inputFrame.Delta);
            }

            float deltaTime = Time.deltaTime;
            orbitRig?.Tick(deltaTime);
            zoomRig?.Tick(deltaTime);
        }

        private void ApplyInput(Vector2 delta)
        {
            if (Mathf.Abs(delta.x) > deadZonePixels && orbitRig != null)
            {
                float direction = invertHorizontal ? -1f : 1f;
                orbitRig.AddYawDelta(delta.x * horizontalDegreesPerPixel * direction);
            }

            if (Mathf.Abs(delta.y) > deadZonePixels && zoomRig != null)
            {
                float direction = invertVerticalZoom ? -1f : 1f;
                zoomRig.AddZoomDelta(delta.y * verticalZoomSizePerPixel * direction);
            }
        }

        private void HandleModeChanged(CameraViewMode mode)
        {
            if (mode == CameraViewMode.City)
            {
                return;
            }

            resolvedInputSource?.CancelGesture();
            orbitRig?.CancelMotion();
            zoomRig?.CancelMotion();
        }

        private void ResolveReferences()
        {
            if (referencesResolved)
            {
                return;
            }

            resolvedInputSource = inputSource as ICameraOrbitInputSource;
            orbitRig = orbitRigSource as IOrbitCameraRig;
            zoomRig = zoomRigSource as ICameraZoomRig;

            if (resolvedInputSource == null)
            {
                resolvedInputSource = GetComponent<ICameraOrbitInputSource>();
            }

            if (orbitRig == null)
            {
                orbitRig = GetComponent<IOrbitCameraRig>();
            }

            if (zoomRig == null)
            {
                zoomRig = GetComponent<ICameraZoomRig>();
            }

            referencesResolved = true;

            if (resolvedInputSource == null)
            {
                Debug.LogWarning("[MapOrbitCameraController] Missing input source.", this);
            }

            if (orbitRig == null)
            {
                Debug.LogWarning("[MapOrbitCameraController] Missing orbit rig.", this);
            }

            if (zoomRig == null)
            {
                Debug.LogWarning("[MapOrbitCameraController] Missing zoom rig.", this);
            }
        }
    }
}
