namespace Game.Runtime.Bootstrap.Steps
{
    using System.Threading;
    using Cysharp.Threading.Tasks;
    using Game.Runtime.Scenes;
    using UnityEngine;
    using UnityEngine.AddressableAssets;

    [CreateAssetMenu(menuName = "CoinDreams/Bootstrap/Load Persistent Scene")]
    public sealed class LoadPersistentSceneStep : BootstrapStepAsset
    {
        [SerializeField] private AssetReference persistentScene;

        public override async UniTask ExecuteAsync(IBootContext context, CancellationToken cancellationToken)
        {
            // Activate on load so the Persistent scene's Awakes can run.
            // FirebasePersistenceBootstrap needs to Awake to start its init pipeline.
            SceneHandle handle = await context.SceneLoader.LoadAdditiveAsync(
                persistentScene,
                true,
                context.StepProgress,
                cancellationToken);

            context.PersistentSceneHandle = handle;
        }
    }
}
