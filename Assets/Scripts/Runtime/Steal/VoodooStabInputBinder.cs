#nullable enable

using Game.Composition.Signals;
using MessagePipe;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.Runtime.Steal
{
    /// <summary>
    /// Deprecated. Previously bound a dedicated stab button (yellow doll square on the Canvas
    /// placeholder) to <c>VoodooStabRequestedSignal</c>. The placeholder UI is gone and the
    /// Draw button now doubles as the stab input via
    /// <see cref="Game.Runtime.Cards.DrawButtonRouter"/>, so this binder is no longer wired by
    /// any setup script. The class is kept temporarily to avoid breaking scenes that still
    /// reference it; remove the component and delete this file in a future cleanup.
    /// </summary>
    [System.Obsolete("VoodooStabInputBinder is superseded by Game.Runtime.Cards.DrawButtonRouter. Remove from your scene; this component will be deleted in a future cleanup.")]
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
