#nullable enable

using System;
using Game.Composition.Signals;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace Game.Runtime.UI.Panels
{
    /// <summary>
    /// Hides a set of GameObjects whenever the navigator reports that some
    /// panel is open, and shows them again when no panel is current. Drop
    /// one of these on a top-level Canvas GameObject and drag the always-
    /// visible UI (right-side rail, action panel, HUD) into the list.
    ///
    /// SRP — only translates the navigator's visibility signal into SetActive
    /// calls on a configured list. Doesn't know which panel triggered the
    /// state change, doesn't know what's IN the list. Pure mediator.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PanelBackgroundVisibilityController : MonoBehaviour
    {
        [Tooltip("GameObjects to SetActive(false) whenever any panel is open, " +
            "and SetActive(true) when no panel is current. Order doesn't matter; " +
            "list duplicates are harmless.")]
        [SerializeField] private GameObject[] hideWhenAnyPanelOpen = Array.Empty<GameObject>();

        [Inject] private ISubscriber<PanelVisibilityChangedSignal>? subscriber;

        private IDisposable? subscription;

        private void OnEnable()
        {
            if (subscriber == null) return;
            if (subscription != null) return;
            subscription = subscriber.Subscribe(HandleVisibilityChanged);
        }

        private void OnDisable()
        {
            subscription?.Dispose();
            subscription = null;
        }

        private void HandleVisibilityChanged(PanelVisibilityChangedSignal signal)
        {
            bool shouldShow = !signal.IsAnyPanelOpen;
            for (int i = 0; i < hideWhenAnyPanelOpen.Length; i++)
            {
                GameObject go = hideWhenAnyPanelOpen[i];
                if (go == null) continue;
                if (go.activeSelf != shouldShow)
                {
                    go.SetActive(shouldShow);
                }
            }
        }
    }
}
