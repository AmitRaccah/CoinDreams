namespace Game.Runtime.Bootstrap.Steps
{
    using System.Threading;
    using Cysharp.Threading.Tasks;
    using UnityEngine;

    [CreateAssetMenu(menuName = "CoinDreams/Bootstrap/Activate Loaded Scenes")]
    public sealed class ActivateLoadedScenesStep : BootstrapStepAsset
    {
        public override async UniTask ExecuteAsync(IBootContext context, CancellationToken cancellationToken)
        {
            if (context.GameplaySceneHandle.IsValid)
            {
                await context.SceneLoader.ActivateAsync(context.GameplaySceneHandle, cancellationToken);
                await context.SceneLoader.SetActiveAsync(context.GameplaySceneHandle);
            }
        }
    }
}
