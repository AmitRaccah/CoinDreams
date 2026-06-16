#nullable enable

using Game.Composition.Signals;
using Game.Domain.Cards;
using MessagePipe;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.Runtime.Cards
{
    [DisallowMultipleComponent]
    public sealed class DrawMultiplierBinder : MonoBehaviour
    {
        [SerializeField] private Button? multiplierButton;
        [SerializeField] private TMP_Text? multiplierLabel;

        [Inject] private IPublisher<MultiplierChangeRequestedSignal>? publisher;

        private int currentIndex;
        private bool wired;

        private void OnEnable()
        {
            if (multiplierButton != null)
            {
                multiplierButton.onClick.AddListener(HandleClick);
                wired = true;
            }

            RefreshLabel();
        }

        private void OnDisable()
        {
            if (!wired) return;
            if (multiplierButton != null)
            {
                multiplierButton.onClick.RemoveListener(HandleClick);
            }
            wired = false;
        }

        private void HandleClick()
        {
            int[] multipliers = AuthoritativeDrawRequest.AllowedMultipliers;
            if (multipliers == null || multipliers.Length == 0)
            {
                return;
            }

            currentIndex = (currentIndex + 1) % multipliers.Length;
            int next = multipliers[currentIndex];

            RefreshLabel();

            if (publisher != null)
            {
                publisher.Publish(new MultiplierChangeRequestedSignal(next));
            }
        }

        private void RefreshLabel()
        {
            if (multiplierLabel == null) return;

            int[] multipliers = AuthoritativeDrawRequest.AllowedMultipliers;
            if (multipliers == null || multipliers.Length == 0) return;

            int safeIndex = currentIndex < 0 || currentIndex >= multipliers.Length ? 0 : currentIndex;
            multiplierLabel.text = "x" + multipliers[safeIndex];
        }
    }
}
