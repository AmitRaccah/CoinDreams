#nullable enable

using Game.Composition.Signals;
using MessagePipe;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;

namespace Game.Runtime.UI.Panels
{
    /// <summary>
    /// Drop-on-button helper: publishes <see cref="PanelCloseRequestedSignal"/>
    /// when its Button is clicked. The navigator decides which panel is
    /// "current" and hides it — the button has no opinion on that.
    ///
    /// SRP: input adapter only. Drop this on any X / Back button inside a
    /// panel; works the same way no matter which panel hosts it.
    ///
    /// Resilient injection: same late-inject fallback as
    /// <see cref="PanelOpenButton"/>. Walks loaded LifetimeScopes at Start
    /// and rebinds if the primary InjectAllInScenes pass missed this
    /// instance (e.g. nested-prefab scope timing race).
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

        private void Start()
        {
            EnsureInjected();
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
                Debug.LogWarning("[PanelCloseButton] No publisher injected — close request dropped.", this);
                return;
            }
            publisher.Publish(new PanelCloseRequestedSignal());
        }

        // Late-inject fallback — see PanelOpenButton.EnsureInjected for
        // rationale. Idempotent; cost bounded by LifetimeScope count.
        private void EnsureInjected()
        {
            if (publisher != null) return;

            LifetimeScope[] scopes = FindObjectsByType<LifetimeScope>(FindObjectsSortMode.None);
            for (int i = 0; i < scopes.Length; i++)
            {
                LifetimeScope scope = scopes[i];
                if (scope == null || scope.Container == null) continue;

                scope.Container.Inject(this);
                if (publisher != null)
                {
#if UNITY_EDITOR
                    Debug.LogWarning(
                        "[PanelCloseButton] Late-injected via '" + scope.name +
                        "' — primary InjectAllInScenes pass missed this instance.",
                        this);
#endif
                    return;
                }
            }
        }
    }
}
