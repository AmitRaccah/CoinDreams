namespace Game.Runtime.Bootstrap
{
    using System.Threading;
    using Cysharp.Threading.Tasks;

    public interface IBootstrapStep
    {
        string DisplayName { get; }
        float Weight { get; }
        UniTask ExecuteAsync(IBootContext context, CancellationToken cancellationToken);
    }
}
