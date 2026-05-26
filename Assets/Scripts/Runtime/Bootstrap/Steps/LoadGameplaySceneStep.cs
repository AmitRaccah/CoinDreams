namespace Game.Runtime.Bootstrap.Steps
{
    using System.Threading;
    using Cysharp.Threading.Tasks;
    using Game.Runtime.Scenes;
    using UnityEngine;
    using UnityEngine.AddressableAssets;

    [CreateAssetMenu(menuName = "CoinDreams/Bootstrap/Load Gameplay Scene")]
    public sealed class LoadGameplaySceneStep : BootstrapStepAsset
    {
        [SerializeField] private AssetReference gameplayScene;

        public override async UniTask ExecuteAsync(IBootContext context, CancellationToken cancellationToken)
        {
            // Load without activation so progress can stream without an immediate frame stall.
            // ActivateLoadedScenesStep promotes it later.
            SceneHandle handle = await context.SceneLoader.LoadAdditiveAsync(
                gameplayScene,
                false,
                context.StepProgress,
                cancellationToken);

            context.GameplaySceneHandle = handle;
        }
    }
}
