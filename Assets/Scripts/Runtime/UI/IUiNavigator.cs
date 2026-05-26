namespace Game.Runtime.UI
{
    using System.Threading;
    using Cysharp.Threading.Tasks;

    public interface IUiNavigator
    {
        UniTask PushPanelAsync(string panelKey, object parameters = null, CancellationToken ct = default);
        UniTask PopPanelAsync(CancellationToken ct = default);
        bool IsTransitioning { get; }
    }
}
