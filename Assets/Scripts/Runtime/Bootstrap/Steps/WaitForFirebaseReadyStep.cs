namespace Game.Runtime.Bootstrap.Steps
{
    using System.Threading;
    using Cysharp.Threading.Tasks;
    using Game.Infrastructure.Persistence;
    using UnityEngine;

    [CreateAssetMenu(menuName = "CoinDreams/Bootstrap/Wait For Firebase Ready")]
    public sealed class WaitForFirebaseReadyStep : BootstrapStepAsset
    {
        public override async UniTask ExecuteAsync(IBootContext context, CancellationToken cancellationToken)
        {
            FirebasePersistenceBootstrap bootstrap = null;
            await UniTask.WaitUntil(
                () =>
                {
                    if (bootstrap == null)
                    {
                        bootstrap = Object.FindAnyObjectByType<FirebasePersistenceBootstrap>();
                    }

                    return bootstrap != null && bootstrap.IsReady;
                },
                cancellationToken: cancellationToken);
        }
    }
}
