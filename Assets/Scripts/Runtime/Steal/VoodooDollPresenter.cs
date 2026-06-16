#nullable enable

using System;
using Game.Composition.Signals;
using MessagePipe;
using TMPro;
using UnityEngine;
using VContainer;

namespace Game.Runtime.Steal
{
    [DisallowMultipleComponent]
    public sealed class VoodooDollPresenter : MonoBehaviour
    {
        private const int StatusSuccess = 0;
        private const int StatusVictimEmpty = 4;
        private const float StolenAmountVisibleSeconds = 1.5f;

        [Header("Needles")]
        [SerializeField] private GameObject[] needleVisuals = Array.Empty<GameObject>();

        [Header("Doll Roots")]
        [SerializeField] private GameObject dollIntactRoot = null!;
        [SerializeField] private GameObject dollBrokenRoot = null!;

        [Header("Stolen Amount Floating Text")]
        [SerializeField] private TMP_Text stolenAmountText = null!;

        [Inject] private ISubscriber<VoodooSessionStartedSignal>? sessionStartedSubscriber;
        [Inject] private ISubscriber<VoodooStabResolvedSignal>? stabResolvedSubscriber;
        [Inject] private ISubscriber<VoodooSessionEndedSignal>? sessionEndedSubscriber;

        private IDisposable? sessionStartedSubscription;
        private IDisposable? stabResolvedSubscription;
        private IDisposable? sessionEndedSubscription;

        private int currentMaxStabs;

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

            CancelInvoke(nameof(HideStolenAmountText));
            HideStolenAmountText();
        }

        private void HandleSessionStarted(VoodooSessionStartedSignal signal)
        {
            currentMaxStabs = signal.MaxStabs;

            UpdateNeedleVisuals(0);
            SetDollBroken(false);

            CancelInvoke(nameof(HideStolenAmountText));
            HideStolenAmountText();
        }

        private void HandleStabResolved(VoodooStabResolvedSignal signal)
        {
            if (signal.Status != StatusSuccess && signal.Status != StatusVictimEmpty)
            {
                return;
            }

            int needlesUsed = currentMaxStabs - signal.StabsRemaining;
            UpdateNeedleVisuals(needlesUsed);

            ShowStolenAmount(signal.StolenAmount);

            if (signal.IsDollBroken)
            {
                SetDollBroken(true);
            }
        }

        private void HandleSessionEnded(VoodooSessionEndedSignal signal)
        {
            CancelInvoke(nameof(HideStolenAmountText));
            HideStolenAmountText();
        }

        private void UpdateNeedleVisuals(int needlesUsed)
        {
            if (needleVisuals == null)
            {
                return;
            }

            int clamped = Mathf.Clamp(needlesUsed, 0, needleVisuals.Length);

            for (int i = 0; i < needleVisuals.Length; i++)
            {
                GameObject needle = needleVisuals[i];
                if (needle == null)
                {
                    continue;
                }

                needle.SetActive(i < clamped);
            }
        }

        private void SetDollBroken(bool broken)
        {
            if (dollIntactRoot != null)
            {
                dollIntactRoot.SetActive(!broken);
            }

            if (dollBrokenRoot != null)
            {
                dollBrokenRoot.SetActive(broken);
            }
        }

        private void ShowStolenAmount(int amount)
        {
            if (stolenAmountText == null)
            {
                return;
            }

            stolenAmountText.SetText("+{0:0}", amount);
            stolenAmountText.gameObject.SetActive(true);

            CancelInvoke(nameof(HideStolenAmountText));
            Invoke(nameof(HideStolenAmountText), StolenAmountVisibleSeconds);
        }

        private void HideStolenAmountText()
        {
            if (stolenAmountText == null)
            {
                return;
            }

            stolenAmountText.gameObject.SetActive(false);
        }
    }
}
