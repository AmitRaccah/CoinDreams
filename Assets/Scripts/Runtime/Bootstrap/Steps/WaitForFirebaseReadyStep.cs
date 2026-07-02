namespace Game.Runtime.Bootstrap.Steps
{
    using System;
    using System.Threading;
    using Cysharp.Threading.Tasks;
    using Game.Infrastructure.Persistence;
    using UnityEngine;

    [CreateAssetMenu(menuName = "CoinDreams/Bootstrap/Wait For Firebase Ready")]
    public sealed class WaitForFirebaseReadyStep : BootstrapStepAsset
    {
        // Hard ceiling so a device with no network / failed anonymous auth
        // surfaces an error on the splash instead of hanging on "Connecting…"
        // forever (FirebasePersistenceBootstrap leaves IsReady false on a failed
        // init with no error flag). Generous enough not to false-trip a slow
        // connection; on timeout the thrown exception routes to splash ShowError.
        private const float ReadyTimeoutSeconds = 30f;

        public override async UniTask ExecuteAsync(IBootContext context, CancellationToken cancellationToken)
        {
            FirebasePersistenceBootstrap bootstrap = null;
            try
            {
                await UniTask.WaitUntil(
                    () =>
                    {
                        if (bootstrap == null)
                        {
                            bootstrap = UnityEngine.Object.FindAnyObjectByType<FirebasePersistenceBootstrap>();
                        }

                        return bootstrap != null && bootstrap.IsReady;
                    },
                    cancellationToken: cancellationToken)
                    .Timeout(TimeSpan.FromSeconds(ReadyTimeoutSeconds));
            }
            catch (TimeoutException)
            {
                throw new TimeoutException(
                    "Firebase did not become ready within " + ReadyTimeoutSeconds
                    + "s — check the network connection and anonymous-auth configuration.");
            }
        }
    }
}
