namespace Game.Runtime.Bootstrap.Steps
{
    using System.Threading;
    using Cysharp.Threading.Tasks;
    using UnityEngine;

    [CreateAssetMenu(menuName = "CoinDreams/Bootstrap/Wait For Profile Loaded")]
    public sealed class WaitForProfileLoadedStep : BootstrapStepAsset
    {
        // TODO: Replace with profile-specific wait once FirebasePlayerPersistenceRuntime
        // exposes IsProfileLoaded. Today IsReady already gates on loadCompleted, so this
        // step is effectively a no-op kept for designer-facing clarity in the boot sequence.
        public override async UniTask ExecuteAsync(IBootContext context, CancellationToken cancellationToken)
        {
            await UniTask.Yield(cancellationToken);
        }
    }
}
