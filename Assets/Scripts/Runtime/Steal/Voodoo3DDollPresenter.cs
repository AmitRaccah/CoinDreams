#nullable enable

using System;
using Game.Composition.Signals;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace Game.Runtime.Steal
{
    /// <summary>
    /// 3D-scene presenter for the voodoo doll. The doll root is a plain
    /// transform — start as a cube placeholder, swap to real art whenever.
    /// Show/hide is driven by session lifecycle signals; per-stab feedback
    /// is a brief color flash on the doll's renderers (no animation yet).
    ///
    /// SOLID note: this class only talks to signals + the doll's transform
    /// hierarchy. The session lifecycle (server calls, multiplier handling)
    /// lives entirely in VoodooStealCoordinator.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Voodoo3DDollPresenter : MonoBehaviour
    {
        private const int StatusSuccess = 0;
        private const int StatusVictimEmpty = 4;
        private const float StabFlashSeconds = 0.15f;

        [Header("Doll root")]
        [SerializeField] private GameObject dollRoot = null!;

        [Header("Stab feedback")]
        [SerializeField] private Color baseColor = new Color(0.85f, 0.78f, 0.45f);
        [SerializeField] private Color stabFlashColor = new Color(0.9f, 0.2f, 0.2f);
        [SerializeField] private Color brokenColor = new Color(0.4f, 0.4f, 0.4f);

        [Inject] private ISubscriber<VoodooSessionStartedSignal>? sessionStartedSubscriber;
        [Inject] private ISubscriber<VoodooStabResolvedSignal>? stabResolvedSubscriber;
        [Inject] private ISubscriber<VoodooSessionEndedSignal>? sessionEndedSubscriber;

        private IDisposable? sessionStartedSubscription;
        private IDisposable? stabResolvedSubscription;
        private IDisposable? sessionEndedSubscription;

        private Renderer[]? cachedRenderers;
        private MaterialPropertyBlock? propertyBlock;
        private bool isBroken;

        private void Awake()
        {
            if (dollRoot != null)
            {
                dollRoot.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (sessionStartedSubscriber != null && sessionStartedSubscription == null)
            {
                sessionStartedSubscription = sessionStartedSubscriber.Subscribe(HandleSessionStarted);
            }
            if (stabResolvedSubscriber != null && stabResolvedSubscription == null)
            {
                stabResolvedSubscription = stabResolvedSubscriber.Subscribe(HandleStabResolved);
            }
            if (sessionEndedSubscriber != null && sessionEndedSubscription == null)
            {
                sessionEndedSubscription = sessionEndedSubscriber.Subscribe(HandleSessionEnded);
            }
        }

        private void OnDisable()
        {
            sessionStartedSubscription?.Dispose();
            sessionStartedSubscription = null;

            stabResolvedSubscription?.Dispose();
            stabResolvedSubscription = null;

            sessionEndedSubscription?.Dispose();
            sessionEndedSubscription = null;

            CancelInvoke(nameof(RestoreBaseColor));
        }

        private void HandleSessionStarted(VoodooSessionStartedSignal signal)
        {
            isBroken = false;
            if (dollRoot != null)
            {
                dollRoot.SetActive(true);
            }
            CancelInvoke(nameof(RestoreBaseColor));
            ApplyColor(baseColor);
        }

        private void HandleStabResolved(VoodooStabResolvedSignal signal)
        {
            if (signal.Status != StatusSuccess && signal.Status != StatusVictimEmpty)
            {
                return;
            }

            // Every stab — including the final one that breaks the doll —
            // gets a flash. The previous version returned early on broken
            // and the player saw only two flashes (or one, when stabs land
            // within the flash window) instead of three.
            ApplyColor(stabFlashColor);
            CancelInvoke(nameof(RestoreBaseColor));

            if (signal.IsDollBroken)
            {
                isBroken = true;
                // After the flash settles, switch to the permanent broken
                // tint instead of restoring the base color.
                Invoke(nameof(SettleBrokenColor), StabFlashSeconds);
            }
            else
            {
                Invoke(nameof(RestoreBaseColor), StabFlashSeconds);
            }
        }

        private void SettleBrokenColor()
        {
            ApplyColor(brokenColor);
        }

        private void HandleSessionEnded(VoodooSessionEndedSignal signal)
        {
            CancelInvoke(nameof(RestoreBaseColor));
            if (dollRoot != null)
            {
                dollRoot.SetActive(false);
            }
        }

        private void RestoreBaseColor()
        {
            if (isBroken) return;
            ApplyColor(baseColor);
        }

        private void ApplyColor(Color color)
        {
            if (dollRoot == null) return;

            if (cachedRenderers == null)
            {
                cachedRenderers = dollRoot.GetComponentsInChildren<Renderer>(true);
            }
            if (cachedRenderers.Length == 0) return;

            propertyBlock ??= new MaterialPropertyBlock();
            propertyBlock.SetColor("_BaseColor", color);
            propertyBlock.SetColor("_Color", color);

            for (int i = 0; i < cachedRenderers.Length; i++)
            {
                Renderer r = cachedRenderers[i];
                if (r == null) continue;
                r.SetPropertyBlock(propertyBlock);
            }
        }
    }
}
