#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Composition.Signals;
using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Runtime.UI.Panels
{
    /// <summary>
    /// Owns the "current visible panel" mutex. Panels self-register at
    /// Awake; the navigator dispatches PanelOpenRequestedSignal /
    /// PanelCloseRequestedSignal to the matching panel's Show / Hide.
    ///
    /// Single-panel model on purpose: opening B while A is visible always
    /// hides A first. No stack, no Back button. Simpler than a generic
    /// navigation stack and fits the side-rail-of-buttons UX.
    ///
    /// SRP — only mutex + dispatch. The panels themselves know how to
    /// appear / disappear (and tomorrow how to animate); the buttons only
    /// know how to publish signals. Each layer ignores the others.
    /// </summary>
    public sealed class PanelNavigator : IInitializable, IDisposable
    {
        private readonly ISubscriber<PanelOpenRequestedSignal> openSubscriber;
        private readonly ISubscriber<PanelCloseRequestedSignal> closeSubscriber;
        private readonly IPublisher<PanelVisibilityChangedSignal> visibilityPublisher;
        private readonly Dictionary<string, IPanel> panels = new Dictionary<string, IPanel>();
        private readonly CancellationTokenSource lifetimeCts = new CancellationTokenSource();

        private IDisposable? openSubscription;
        private IDisposable? closeSubscription;
        private IPanel? currentPanel;
        private bool transitioning;
        private bool lastVisibilityState;

        [Inject]
        public PanelNavigator(
            ISubscriber<PanelOpenRequestedSignal> openSubscriber,
            ISubscriber<PanelCloseRequestedSignal> closeSubscriber,
            IPublisher<PanelVisibilityChangedSignal> visibilityPublisher)
        {
            this.openSubscriber = openSubscriber;
            this.closeSubscriber = closeSubscriber;
            this.visibilityPublisher = visibilityPublisher;
        }

        public void Initialize()
        {
            openSubscription = openSubscriber.Subscribe(s => OpenAsync(s.PanelKey).Forget());
            closeSubscription = closeSubscriber.Subscribe(_ => CloseAsync().Forget());
        }

        public void Dispose()
        {
            openSubscription?.Dispose();
            openSubscription = null;
            closeSubscription?.Dispose();
            closeSubscription = null;

            if (!lifetimeCts.IsCancellationRequested)
            {
                lifetimeCts.Cancel();
            }
            lifetimeCts.Dispose();
        }

        /// <summary>
        /// Panels call this from their Awake. Last-registration-wins for
        /// duplicate keys — log a warning so the conflict is visible.
        /// </summary>
        public void Register(IPanel panel)
        {
            if (panel == null) return;
            string key = panel.PanelKey;
            if (string.IsNullOrWhiteSpace(key))
            {
                Debug.LogWarning("[PanelNavigator] Ignoring panel with empty PanelKey.");
                return;
            }

            if (panels.TryGetValue(key, out IPanel existing) && existing != panel)
            {
                Debug.LogWarning("[PanelNavigator] Duplicate panel key '" + key
                    + "' — overwriting previous registration.");
            }
            panels[key] = panel;
        }

        public void Unregister(IPanel panel)
        {
            if (panel == null) return;
            if (panels.TryGetValue(panel.PanelKey, out IPanel existing) && existing == panel)
            {
                panels.Remove(panel.PanelKey);
            }
            if (currentPanel == panel)
            {
                currentPanel = null;
            }
        }

        private async UniTask OpenAsync(string panelKey)
        {
            if (transitioning) return; // ignore re-entry while animating
            if (!panels.TryGetValue(panelKey, out IPanel next))
            {
                Debug.LogWarning("[PanelNavigator] No panel registered for key '" + panelKey + "'.");
                return;
            }
            if (currentPanel == next) return;

            transitioning = true;
            try
            {
                CancellationToken ct = lifetimeCts.Token;
                if (currentPanel != null)
                {
                    await currentPanel.HideAsync(ct);
                }
                currentPanel = next;
                await currentPanel.ShowAsync(ct);
                PublishVisibility();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Debug.LogError("[PanelNavigator] OpenAsync threw: " + ex);
            }
            finally
            {
                transitioning = false;
            }
        }

        private async UniTask CloseAsync()
        {
            if (transitioning) return;
            if (currentPanel == null) return;

            transitioning = true;
            try
            {
                await currentPanel.HideAsync(lifetimeCts.Token);
                currentPanel = null;
                PublishVisibility();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Debug.LogError("[PanelNavigator] CloseAsync threw: " + ex);
            }
            finally
            {
                transitioning = false;
            }
        }

        // Publishes the current visibility state so background-UI controllers
        // can react. We publish on every state-affecting transition (open
        // from idle, swap A→B, close to idle) — receivers are expected to be
        // idempotent (SetActive is). The lastVisibilityState guard avoids
        // republishing when the boolean side hasn't actually changed (panel
        // swap keeps IsAnyPanelOpen=true), but key changes still fire so
        // per-key listeners can adapt.
        private void PublishVisibility()
        {
            bool isOpen = currentPanel != null;
            string key = currentPanel != null ? currentPanel.PanelKey : string.Empty;
            visibilityPublisher.Publish(new PanelVisibilityChangedSignal(isOpen, key));
            lastVisibilityState = isOpen;
        }
    }
}
