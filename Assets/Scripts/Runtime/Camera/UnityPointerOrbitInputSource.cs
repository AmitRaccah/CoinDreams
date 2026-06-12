#nullable enable

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Game.Runtime.Cameras
{
    [DisallowMultipleComponent]
    public sealed class UnityPointerOrbitInputSource : MonoBehaviour, ICameraOrbitInputSource
    {
        private enum PointerKind
        {
            None = 0,
            Touch = 1,
            Mouse = 2
        }

        [Header("References")]
        [SerializeField] private MonoBehaviour? inputBlockerSource;

        [Header("Mouse")]
        [SerializeField] private bool enableMouseDrag = true;

        private ICameraInputBlocker? inputBlocker;
        private PointerKind activePointerKind;
        private bool gestureActive;
        private bool gestureBlocked;
        private Vector2 previousPosition;

        private void Awake()
        {
            ResolveReferences();
        }

        public OrbitCameraInputFrame ReadFrame()
        {
            ResolveReferences();

            if (activePointerKind == PointerKind.Touch
                && TryReadTouch(out Vector2 activeTouchPosition, out bool activeTouchPressed))
            {
                return ReadPointerFrame(activeTouchPosition, activeTouchPressed, PointerKind.Touch);
            }

            if (activePointerKind == PointerKind.Mouse
                && TryReadMouse(out Vector2 activeMousePosition, out bool activeMousePressed))
            {
                return ReadPointerFrame(activeMousePosition, activeMousePressed, PointerKind.Mouse);
            }

            if (TryReadTouch(out Vector2 touchPosition, out bool touchPressed) && touchPressed)
            {
                return ReadPointerFrame(touchPosition, true, PointerKind.Touch);
            }

            if (enableMouseDrag
                && TryReadMouse(out Vector2 mousePosition, out bool mousePressed)
                && mousePressed)
            {
                return ReadPointerFrame(mousePosition, true, PointerKind.Mouse);
            }

            CancelGesture();
            return OrbitCameraInputFrame.None;
        }

        public void CancelGesture()
        {
            activePointerKind = PointerKind.None;
            gestureActive = false;
            gestureBlocked = false;
            previousPosition = Vector2.zero;
        }

        private OrbitCameraInputFrame ReadPointerFrame(Vector2 position, bool pressed, PointerKind pointerKind)
        {
            if (!pressed)
            {
                CancelGesture();
                return OrbitCameraInputFrame.None;
            }

            if (!gestureActive)
            {
                activePointerKind = pointerKind;
                gestureActive = true;
                gestureBlocked = inputBlocker != null && inputBlocker.IsBlocked(position);
                previousPosition = position;
                return OrbitCameraInputFrame.None;
            }

            Vector2 delta = position - previousPosition;
            previousPosition = position;

            if (gestureBlocked)
            {
                return OrbitCameraInputFrame.None;
            }

            return delta.sqrMagnitude > 0f
                ? new OrbitCameraInputFrame(true, delta)
                : OrbitCameraInputFrame.None;
        }

        private bool TryReadTouch(out Vector2 position, out bool pressed)
        {
            position = Vector2.zero;
            pressed = false;

            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen == null)
            {
                return false;
            }

            TouchControl touch = touchscreen.primaryTouch;
            if (touch == null)
            {
                return false;
            }

            pressed = touch.press.isPressed;
            position = touch.position.ReadValue();
            return pressed || gestureActive;
        }

        private bool TryReadMouse(out Vector2 position, out bool pressed)
        {
            position = Vector2.zero;
            pressed = false;

            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return false;
            }

            pressed = mouse.leftButton.isPressed;
            position = mouse.position.ReadValue();
            return pressed || gestureActive;
        }

        private void ResolveReferences()
        {
            if (inputBlocker == null && inputBlockerSource != null)
            {
                inputBlocker = inputBlockerSource as ICameraInputBlocker;
            }

            if (inputBlocker == null)
            {
                inputBlocker = GetComponent<ICameraInputBlocker>();
            }
        }
    }
}
