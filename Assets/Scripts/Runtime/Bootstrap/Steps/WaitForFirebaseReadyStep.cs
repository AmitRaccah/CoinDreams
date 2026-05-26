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
            FirebasePlayerPersistenceRuntime runtime = null;
            await UniTask.WaitUntil(
                () =>
                {
                    if (runtime == null)
                    {
                        runtime = Object.FindAnyObjectByType<FirebasePlayerPersistenceRuntime>();
                    }

                    return runtime != null && runtime.IsReady;
                },
                cancellationToken: cancellationToken);
        }
    }
}
