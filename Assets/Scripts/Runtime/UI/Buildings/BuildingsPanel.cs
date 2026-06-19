#nullable enable

using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Runtime.UI.Panels;
using UnityEngine;
using VContainer;

namespace Game.Runtime.UI.Buildings
{
    /// <summary>
    /// IPanel implementation for the Buildings panel. Owns nothing but its
    /// own GameObject's active state — the actual content rendering lives
    /// in <see cref="BuildingsPanelPresenter"/> on the same GameObject, so
    /// Show / Hide flip both visibility AND the presenter's OnEnable /
    /// OnDisable lifecycle (which is when the presenter refreshes the
    /// view from the latest player state).
    ///
    /// IMPORTANT — the panel's GameObject MUST be enabled in the scene at
    /// edit time. Start() registers with the navigator and then hides the
    /// panel; if it starts disabled, Start never fires and registration
    /// never happens.
    ///
    /// SRP — only show / hide + navigator handshake. No content logic.
    /// Animation hooks slot inside ShowAsync / HideAsync without
    /// touching the presenter or the navigator.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BuildingsPanel : MonoBehaviour, IPanel
    {
        [Tooltip("Must match the PanelKey used by PanelOpenButton wired to the side-rail button.")]
        [SerializeField] private string panelKey = "buildings";

        [Inject] private PanelNavigator? navigator;

        private bool registered;

        public string PanelKey => panelKey;

        private void Start()
        {
            if (navigator == null)
            {
                Debug.LogWarning("[BuildingsPanel] PanelNavigator not injected; panel will not register.");
                return;
            }
            navigator.Register(this);
            registered = true;
            // Hide AFTER registering — registration must happen while the
            // GameObject is active (Start only fires on active GOs).
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (registered && navigator != null)
            {
                navigator.Unregister(this);
            }
        }

        public UniTask ShowAsync(CancellationToken ct)
        {
            gameObject.SetActive(true);
            return UniTask.CompletedTask;
        }

        public UniTask HideAsync(CancellationToken ct)
        {
            gameObject.SetActive(false);
            return UniTask.CompletedTask;
        }
    }
}
