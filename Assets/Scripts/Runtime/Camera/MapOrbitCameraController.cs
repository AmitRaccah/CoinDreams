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
        [SerializeField] private MonoBehaviour? panInputSource;
        [SerializeField] private MonoBehaviour? panRigSource;

        [Header("Sensitivity")]
        [SerializeField] private float horizontalDegreesPerPixel = 0.08f;
        [SerializeField] private float verticalZoomSizePerPixel = 0.004f;
        [SerializeField] private float horizontalPanWorldUnitsPerPixel = 0.01f;

        [Header("Axes")]
        [SerializeField] private bool invertHorizontal;
        [SerializeField] private bool invertVerticalZoom;
        [SerializeField] private bool invertPanHorizontal;

        [Header("Filtering")]
        [SerializeField] private float deadZonePixels = 0.01f;

        [Inject] private ICameraViewModeReader? viewModeReader;

        private ICameraOrbitInputSource? resolvedInputSource;
        private IOrbitCameraRig? orbitRig;
        private ICameraZoomRig? zoomRig;
        private IPanInputSource? resolvedPanInputSource;
        private IPanRig? panRig;
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
            resolvedPanInputSource?.CancelGesture();
            orbitRig?.CancelMotion();
            zoomRig?.CancelMotion();
            panRig?.CancelMotion();
        }

        private void Update()
        {
            ResolveReferences();

            // The rigs unconditionally rewrite rigRoot.position/rotation in ApplyCurrentYaw/Pan
            // every frame based on the initialOffset captured at Awake. When CameraTransitionService
            // drives the camera to the board anchor, ticking the rigs would immediately overwrite
            // that position next frame and snap the camera back to the city pose. While the camera
            // is not in CityView, the transition service owns the transform — stay out of its way.
            bool ownsCamera = viewModeReader == null || viewModeReader.IsCityView;
            if (!ownsCamera)
            {
                return;
            }

            OrbitCameraInputFrame orbitFrame = resolvedInputSource != null
                ? resolvedInputSource.ReadFrame()
                : OrbitCameraInputFrame.None;

            if (orbitFrame.HasInput)
            {
                ApplyOrbitInput(orbitFrame.Delta);
            }

            PanInputFrame panFrame = resolvedPanInputSource != null
                ? resolvedPanInputSource.ReadFrame()
                : PanInputFrame.None;

            if (panFrame.HasInput)
            {
                ApplyPanInput(panFrame.Delta);
            }

            float deltaTime = Time.deltaTime;
            panRig?.Tick(deltaTime);
            orbitRig?.Tick(deltaTime);
            zoomRig?.Tick(deltaTime);
        }

        private void ApplyOrbitInput(Vector2 delta)
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

        private void ApplyPanInput(Vector2 delta)
        {
            if (panRig == null || Mathf.Abs(delta.x) <= deadZonePixels)
            {
                return;
            }

            float direction = invertPanHorizontal ? -1f : 1f;
            panRig.AddPanDelta(delta.x * horizontalPanWorldUnitsPerPixel * direction);
        }

        private void HandleModeChanged(CameraViewMode mode)
        {
            if (mode == CameraViewMode.City)
            {
                return;
            }

            resolvedInputSource?.CancelGesture();
            resolvedPanInputSource?.CancelGesture();
            orbitRig?.CancelMotion();
            zoomRig?.CancelMotion();
            panRig?.CancelMotion();
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
            resolvedPanInputSource = panInputSource as IPanInputSource;
            panRig = panRigSource as IPanRig;

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

            if (resolvedPanInputSource == null)
            {
                resolvedPanInputSource = GetComponent<IPanInputSource>();
            }

            if (panRig == null)
            {
                panRig = GetComponent<IPanRig>();
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

            if (resolvedPanInputSource == null && panInputSource != null)
            {
                Debug.LogWarning("[MapOrbitCameraController] Pan input source assigned but does not implement IPanInputSource.", this);
            }

            if (panRig == null && panRigSource != null)
            {
                Debug.LogWarning("[MapOrbitCameraController] Pan rig assigned but does not implement IPanRig.", this);
            }
        }
    }
}
