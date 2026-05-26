#nullable enable

using Game.Runtime.Cards;
using UnityEngine;

namespace Game.Composition
{
    // Binder lives in the Persistent scene; controller lives in the Gameplay scene.
    // Persistent scope's container builds before Gameplay scene loads, so we cannot bind
    // to the controller instance directly at registration time. Resolve lazily on first
    // use; re-resolve if the cached reference is destroyed (Unity-overloaded ==).
    internal sealed class LazyCardDrawWorkflowProxy : ICardDrawWorkflowCommands
    {
        private CardDrawWorkflowController? cached;

        public void RequestDraw()
        {
            CardDrawWorkflowController? controller = this.Resolve();
            if (controller == null)
            {
                return;
            }

            controller.RequestDraw();
        }

        public void RequestReturn()
        {
            CardDrawWorkflowController? controller = this.Resolve();
            if (controller == null)
            {
                return;
            }

            controller.RequestReturn();
        }

        private CardDrawWorkflowController? Resolve()
        {
            if (this.cached != null)
            {
                return this.cached;
            }

            this.cached = Object.FindAnyObjectByType<CardDrawWorkflowController>();
            if (this.cached == null)
            {
                Debug.LogWarning(
                    "[LazyCardDrawWorkflowProxy] CardDrawWorkflowController not found in any loaded scene. " +
                    "Is the Gameplay scene loaded?");
            }

            return this.cached;
        }
    }
}
