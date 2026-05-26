using UnityEngine;
using UnityEngine.UI;
using Game.Runtime;

namespace Game.Runtime.Cards
{
    [DisallowMultipleComponent]
    public sealed class CardDrawHudInputBinder : MonoBehaviour
    {
        [SerializeField] private Button drawButton;
        [SerializeField] private Button returnButton;

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
            ICardDrawWorkflowCommands workflow = ResolveWorkflow();
            if (workflow == null) return;
            workflow.RequestDraw();
        }

        private void HandleReturn()
        {
            ICardDrawWorkflowCommands workflow = ResolveWorkflow();
            if (workflow == null) return;
            workflow.RequestReturn();
        }

        private ICardDrawWorkflowCommands ResolveWorkflow()
        {
            if (!RuntimeServiceResolver.TryResolveDrawWorkflowCommands(
                    null, out ICardDrawWorkflowCommands commands, out _))
            {
                Debug.LogWarning(
                    "[CardDrawHudInputBinder] No ICardDrawWorkflowCommands in scene. " +
                    "Is 02_Gameplay loaded with a CardDrawWorkflowController?",
                    this);
                return null;
            }
            return commands;
        }
    }
}
