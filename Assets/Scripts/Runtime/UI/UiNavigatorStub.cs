namespace Game.Runtime.UI
{
    using System.Threading;
    using Cysharp.Threading.Tasks;
    using UnityEngine;

    public sealed class UiNavigatorStub : IUiNavigator
    {
        public bool IsTransitioning
        {
            get { return false; }
        }

        public UniTask PushPanelAsync(string panelKey, object parameters = null, CancellationToken ct = default)
        {
            Debug.Log("[UiNavigator] TODO PushPanelAsync: " + (panelKey ?? "<null>"));
            return UniTask.CompletedTask;
        }

        public UniTask PopPanelAsync(CancellationToken ct = default)
        {
            Debug.Log("[UiNavigator] TODO PopPanelAsync");
            return UniTask.CompletedTask;
        }
    }
}
