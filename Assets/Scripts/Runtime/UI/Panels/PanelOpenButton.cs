#nullable enable

using Game.Composition.Signals;
using MessagePipe;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.Runtime.UI.Panels
{
    /// <summary>
    /// Drop-on-button helper: publishes <see cref="PanelOpenRequestedSignal"/>
    /// with the configured <see cref="panelKey"/> whenever the bound Button
    /// is clicked. Pure adapter from UnityEngine.UI to MessagePipe — knows
    /// nothing about the navigator or about other panels.
    ///
    /// SRP: input adapter only. To open a different panel, change the key
    /// in the Inspector; the navigator's dispatch table does the rest.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class PanelOpenButton : MonoBehaviour
    {
        [Tooltip("Must match the IPanel.PanelKey of the target panel.")]
        [SerializeField] private string panelKey = string.Empty;

        [Inject] private IPublisher<PanelOpenRequestedSignal>? publisher;

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
                Debug.LogWarning("[PanelOpenButton] No publisher injected — open request dropped.");
                return;
            }
            publisher.Publish(new PanelOpenRequestedSignal(panelKey));
        }
    }
}
