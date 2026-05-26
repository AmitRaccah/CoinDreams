#nullable enable

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

        [Inject] private ICardDrawWorkflowCommands? workflow;

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
            if (workflow == null)
            {
                Debug.LogWarning(
                    "[CardDrawHudInputBinder] Workflow not injected. Is GameplayLifetimeScope active?",
                    this);
                return;
            }
            workflow.RequestDraw();
        }

        private void HandleReturn()
        {
            if (workflow == null)
            {
                Debug.LogWarning(
                    "[CardDrawHudInputBinder] Workflow not injected. Is GameplayLifetimeScope active?",
                    this);
                return;
            }
            workflow.RequestReturn();
        }
    }
}
