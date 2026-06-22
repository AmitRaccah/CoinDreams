#nullable enable

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Runtime.UI.Panels;
using MoreMountains.Feedbacks;
using UnityEngine;
using VContainer;

namespace Game.Runtime.UI.Buildings
{
    /// <summary>
    /// IPanel implementation for the Buildings panel. Owns nothing but its
    /// own GameObject's active state and the open/close feel feedbacks —
    /// the actual content rendering lives in <see cref="BuildingsPanelPresenter"/>
    /// on the same GameObject.
    ///
    /// Lifecycle:
    ///   ShowAsync — SetActive(true) → play Open feedbacks → await completion.
    ///   HideAsync — play Close feedbacks → await completion → SetActive(false).
    ///
    /// Industry-standard pattern: keep the GameObject inactive when not
    /// visible so raycasts, layout rebuilds, and stray clicks all stop.
    /// The 5 transform-animated UI parts (ActionPanel, PanelImageHolder,
    /// CloseHolder, BuildButton, PanelHolder) live on the Canvas root, not
    /// inside the panel, so the side-rail BuildButton can choreograph its
    /// slide independently of the panel's enabled state.
    ///
    /// SRP — show / hide + navigator handshake + feedback orchestration.
    /// No content logic.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BuildingsPanel : MonoBehaviour, IPanel
    {
        [Tooltip("Must match the PanelKey used by PanelOpenButton wired to the side-rail button.")]
        [SerializeField] private string panelKey = "buildings";

        [Header("Feel Feedbacks")]
        [Tooltip("MMF_Player on BuildPanelOpenFeedbacks. Plays slide-in choreography. " +
            "Optional — if null, the panel just appears without animation.")]
        [SerializeField] private MMF_Player? openFeedbacks;
        [Tooltip("MMF_Player on BuildPanelCloseFeedbacks. Plays slide-out choreography. " +
            "Optional — if null, the panel just disappears without animation.")]
        [SerializeField] private MMF_Player? closeFeedbacks;

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

        public async UniTask ShowAsync(CancellationToken ct)
        {
            // GameObject must be active BEFORE feedbacks play — children that
            // animate need their own enabled state to render the slide-in.
            gameObject.SetActive(true);
            if (openFeedbacks == null) return;

            openFeedbacks.PlayFeedbacks();
            await WaitForFeedbacks(openFeedbacks, ct);
        }

        public async UniTask HideAsync(CancellationToken ct)
        {
            if (closeFeedbacks != null)
            {
                closeFeedbacks.PlayFeedbacks();
                await WaitForFeedbacks(closeFeedbacks, ct);
            }
            // Deactivate AFTER the close animation finishes — keeps raycasts
            // off, layout dormant, and prevents accidental mid-fade clicks.
            gameObject.SetActive(false);
        }

        // Wait by total duration rather than polling IsPlaying. IsPlaying
        // can still be false on the same frame PlayFeedbacks was called
        // (the player ticks IsPlaying on the next frame), which made
        // WaitUntil(!IsPlaying) return immediately and cause SetActive(false)
        // to race the animation — symptom was "Close animation doesn't run,
        // panel just snaps back to original".
        private static async UniTask WaitForFeedbacks(MMF_Player feedbacks, CancellationToken ct)
        {
            float duration = feedbacks.TotalDuration;
            if (duration <= 0f) return;
            await UniTask.Delay(TimeSpan.FromSeconds(duration), cancellationToken: ct);
        }
    }
}
