namespace Game.Runtime.Scenes
{
    using System;
    using System.Threading;
    using Cysharp.Threading.Tasks;
    using UnityEngine;
    using UnityEngine.AddressableAssets;
    using UnityEngine.ResourceManagement.AsyncOperations;
    using UnityEngine.ResourceManagement.ResourceProviders;
    using UnityEngine.SceneManagement;

    public sealed class UnitySceneLoader : ISceneLoader
    {
        public async UniTask<SceneHandle> LoadAdditiveAsync(
            AssetReference sceneReference,
            bool activateOnLoad,
            IProgress<float> progress = null,
            CancellationToken cancellationToken = default)
        {
            if (sceneReference == null)
            {
                throw new ArgumentNullException(nameof(sceneReference));
            }

            AsyncOperationHandle<SceneInstance> op = Addressables.LoadSceneAsync(
                sceneReference,
                LoadSceneMode.Additive,
                activateOnLoad);

            try
            {
                while (!op.IsDone)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        ReleaseDefensively(op);
                        cancellationToken.ThrowIfCancellationRequested();
                    }

                    if (progress != null)
                    {
                        progress.Report(op.PercentComplete);
                    }

                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                }

                if (op.Status != AsyncOperationStatus.Succeeded)
                {
                    Exception error = op.OperationException ?? new InvalidOperationException(
                        "Addressables.LoadSceneAsync failed.");
                    ReleaseDefensively(op);
                    throw error;
                }

                if (progress != null)
                {
                    progress.Report(1f);
                }

                string key = sceneReference.RuntimeKey != null ? sceneReference.RuntimeKey.ToString() : string.Empty;
                return new SceneHandle(op, key);
            }
            catch (OperationCanceledException)
            {
                ReleaseDefensively(op);
                throw;
            }
        }

        public async UniTask ActivateAsync(SceneHandle handle, CancellationToken cancellationToken = default)
        {
            if (!handle.Operation.IsValid())
            {
                throw new InvalidOperationException("SceneHandle is not valid.");
            }

            AsyncOperation activation = handle.Operation.Result.ActivateAsync();
            if (activation == null)
            {
                return;
            }

            await activation.ToUniTask(cancellationToken: cancellationToken);
        }

        public async UniTask UnloadAsync(SceneHandle handle, CancellationToken cancellationToken = default)
        {
            if (!handle.Operation.IsValid())
            {
                return;
            }

            AsyncOperationHandle<SceneInstance> unloadOp = Addressables.UnloadSceneAsync(handle.Operation);
            while (!unloadOp.IsDone)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }
        }

        public UniTask SetActiveAsync(SceneHandle handle)
        {
            if (!handle.Operation.IsValid())
            {
                throw new InvalidOperationException("SceneHandle is not valid.");
            }

            Scene scene = handle.Scene;
            if (scene.IsValid() && scene.isLoaded)
            {
                SceneManager.SetActiveScene(scene);
            }

            return UniTask.CompletedTask;
        }

        public bool IsLoaded(SceneHandle handle)
        {
            if (!handle.Operation.IsValid())
            {
                return false;
            }

            Scene scene = handle.Scene;
            return scene.IsValid() && scene.isLoaded;
        }

        private static void ReleaseDefensively(AsyncOperationHandle<SceneInstance> op)
        {
            try
            {
                if (op.IsValid())
                {
                    Addressables.Release(op);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[UnitySceneLoader] Failed to release Addressables handle: " + ex.Message);
            }
        }
    }
}
