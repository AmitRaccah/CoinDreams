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
    /// Drop-on-button helper: publishes <see cref="PanelOpenRequestedSignal"/>
    /// with the configured <see cref="panelKey"/> whenever the bound Button
    /// is clicked. Pure adapter from UnityEngine.UI to MessagePipe — knows
    /// nothing about the navigator or about other panels.
    ///
    /// SRP: input adapter only. To open a different panel, change the key
    /// in the Inspector; the navigator's dispatch table does the rest.
    ///
    /// Resilient injection: the primary path is the LifetimeScope's
    /// InjectAllInScenes pass. If that misses (FindObjectsByType race when
    /// the GameObject lives inside a nested prefab the scope didn't see
    /// during its build callback), the publisher is null and clicks would
    /// silently drop. <see cref="EnsureInjected"/> walks every loaded
    /// LifetimeScope at Start and rebinds — once-only cost, no per-click
    /// scan. The Editor-only warning surfaces the recovery so the underlying
    /// scope-timing bug stays visible instead of getting masked.
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
                Debug.LogWarning("[PanelOpenButton] No publisher injected — open request dropped.", this);
                return;
            }
            publisher.Publish(new PanelOpenRequestedSignal(panelKey));
        }

        // Late-inject fallback. Walks loaded LifetimeScopes and asks each to
        // Inject(this) until the publisher field gets populated. Idempotent
        // — re-injection is a no-op once filled, and the scan is bounded by
        // the number of scopes (typically 2 — Persistent + Gameplay).
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
                        "[PanelOpenButton] Late-injected via '" + scope.name +
                        "' — primary InjectAllInScenes pass missed this instance. " +
                        "Investigate the scope's build-callback timing if this recurs.",
                        this);
#endif
                    return;
                }
            }
        }
    }
}
