#nullable enable

using Game.Composition.Signals;
using MessagePipe;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.Runtime.Steal
{
    [DisallowMultipleComponent]
    public sealed class VoodooStabInputBinder : MonoBehaviour
    {
        [SerializeField] private Button? stabButton;

        [Inject] private IPublisher<VoodooStabRequestedSignal>? publisher;

        private void OnEnable()
        {
            if (stabButton == null)
            {
                Debug.LogError("[VoodooStabInputBinder] stabButton is not assigned in the inspector.", this);
                return;
            }
            stabButton.onClick.AddListener(HandleClick);
        }

        private void OnDisable()
        {
            if (stabButton != null)
            {
                stabButton.onClick.RemoveListener(HandleClick);
            }
        }

        private void HandleClick()
        {
            if (publisher == null) return;
            // The binder is intentionally session-agnostic — pass empty sessionId. The coordinator
            // tracks the active session and ignores the click if no session is active.
            publisher.Publish(new VoodooStabRequestedSignal(string.Empty));
        }
    }
}
