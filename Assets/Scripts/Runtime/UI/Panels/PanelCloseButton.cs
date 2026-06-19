#nullable enable

using Game.Composition.Signals;
using MessagePipe;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.Runtime.UI.Panels
{
    /// <summary>
    /// Drop-on-button helper: publishes <see cref="PanelCloseRequestedSignal"/>
    /// when its Button is clicked. The navigator decides which panel is
    /// "current" and hides it — the button has no opinion on that.
    ///
    /// SRP: input adapter only. Drop this on any X / Back button inside a
    /// panel; works the same way no matter which panel hosts it.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class PanelCloseButton : MonoBehaviour
    {
        [Inject] private IPublisher<PanelCloseRequestedSignal>? publisher;

        private Button? button;

        private void Awake()
        {
            button = GetComponent<Button>();
        }

        private void OnEnable()
        {
            if (button != null) button.onClick.AddListener(HandleClicked);
        }

        private void OnDisable()
        {
            if (button != null) button.onClick.RemoveListener(HandleClicked);
        }

        private void HandleClicked()
        {
            if (publisher == null)
            {
                Debug.LogWarning("[PanelCloseButton] No publisher injected — close request dropped.");
                return;
            }
            publisher.Publish(new PanelCloseRequestedSignal());
        }
    }
}
