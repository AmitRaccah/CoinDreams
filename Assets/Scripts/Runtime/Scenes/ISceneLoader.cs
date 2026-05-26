namespace Game.Runtime.Scenes
{
    using System;
    using System.Threading;
    using Cysharp.Threading.Tasks;
    using UnityEngine.AddressableAssets;

    public interface ISceneLoader
    {
        UniTask<SceneHandle> LoadAdditiveAsync(
            AssetReference sceneReference,
            bool activateOnLoad,
            IProgress<float> progress = null,
            CancellationToken cancellationToken = default);

        UniTask ActivateAsync(SceneHandle handle, CancellationToken cancellationToken = default);
        UniTask UnloadAsync(SceneHandle handle, CancellationToken cancellationToken = default);
        UniTask SetActiveAsync(SceneHandle handle);
        bool IsLoaded(SceneHandle handle);
    }
}
