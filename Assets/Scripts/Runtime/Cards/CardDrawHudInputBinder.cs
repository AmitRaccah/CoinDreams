#nullable enable

using Game.Signals;
using MessagePipe;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.Runtime.Cards
{
    [DisallowMultipleComponent]
    public sealed class CardDrawHudInputBinder : MonoBehaviour
    {
        [SerializeField] private Button? drawButton;
        [SerializeField] private Button? returnButton;

        [Inject] private IPublisher<DrawButtonClickedSignal>? drawPublisher;
        [Inject] private IPublisher<ReturnRequestedSignal>? returnPublisher;

        private bool wired;

        private void OnEnable()
        {
            if (drawButton != null) drawButton.onClick.AddListener(HandleDraw);
            if (returnButton != null) returnButton.onClick.AddListener(HandleReturn);
            wired = true;
        }

        private void OnDisable()
        {
            if (!wired) return;
            if (drawButton != null) drawButton.onClick.RemoveListener(HandleDraw);
            if (returnButton != null) returnButton.onClick.RemoveListener(HandleReturn);
            wired = false;
        }

        private void HandleDraw()
        {
            if (drawPublisher == null)
            {
                Debug.LogWarning(
                    "[CardDrawHudInputBinder] Draw publisher not injected. Is PersistentLifetimeScope active?",
                    this);
                return;
            }
            drawPublisher.Publish(new DrawButtonClickedSignal());
        }

        private void HandleReturn()
        {
            if (returnPublisher == null)
            {
                Debug.LogWarning(
                    "[CardDrawHudInputBinder] Return publisher not injected. Is PersistentLifetimeScope active?",
                    this);
                return;
            }
            returnPublisher.Publish(default);
        }
    }
}
