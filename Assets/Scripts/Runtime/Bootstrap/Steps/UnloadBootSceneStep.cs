namespace Game.Runtime.Bootstrap.Steps
{
    using System.Threading;
    using Cysharp.Threading.Tasks;
    using UnityEngine;
    using UnityEngine.SceneManagement;

    [CreateAssetMenu(menuName = "CoinDreams/Bootstrap/Unload Boot Scene")]
    public sealed class UnloadBootSceneStep : BootstrapStepAsset
    {
        [SerializeField] private string bootSceneName = "00_Boot";

        public override async UniTask ExecuteAsync(IBootContext context, CancellationToken cancellationToken)
        {
            // Boot scene was loaded via Build Settings (not Addressables), so unload it
            // directly with SceneManager rather than through ISceneLoader.
            int count = SceneManager.sceneCount;
            int i;
            for (i = 0; i < count; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                bool isBoot = scene.name == bootSceneName || scene.buildIndex == 0;
                if (!isBoot)
                {
                    continue;
                }

                if (context.GameplaySceneHandle.IsValid && context.GameplaySceneHandle.Scene == scene)
                {
                    Debug.LogWarning(
                        "[UnloadBootSceneStep] Boot scene matches gameplay scene; skipping unload.");
                    return;
                }

                AsyncOperation op = SceneManager.UnloadSceneAsync(scene);
                if (op == null)
                {
                    return;
                }

                await op.ToUniTask(cancellationToken: cancellationToken);
                return;
            }
        }
    }
}
