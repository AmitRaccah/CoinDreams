#nullable enable

using Game.Runtime.Cards;
using Game.Runtime.Cameras;
using Game.Runtime.Scenes;
using Game.Runtime.Village;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Composition
{
    public sealed class GameplayLifetimeScope : LifetimeScope
    {
        protected override void Awake()
        {
            if (parentReference.Type == null)
            {
                parentReference = ParentReference.Create<PersistentLifetimeScope>();
            }
            base.Awake();
        }

        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<CameraViewModeService>(Lifetime.Scoped)
                .As<ICameraViewModeReader>()
                .As<ICameraViewModeWriter>()
                .AsSelf();

            // Guard each registration: VContainer's RegisterComponentInHierarchy throws if no
            // matching component exists in the scope's scene. Skipping a missing component lets
            // the rest of the container build instead of taking down the whole scope.
            TryRegisterInHierarchy<MapOrbitCameraController>(builder);
            TryRegisterInHierarchy<CardDrawWorkflowController>(builder);
            TryRegisterInHierarchy<DrawHudPresenter>(builder);
            TryRegisterInHierarchy<DrawActionPresenter>(builder);
            TryRegisterInHierarchy<VillageUpgradeRuntime>(builder);
        }

        private static void TryRegisterInHierarchy<T>(IContainerBuilder builder) where T : Component
        {
            if (Object.FindAnyObjectByType<T>() != null)
            {
                builder.RegisterComponentInHierarchy<T>();
            }
            else
            {
                Debug.LogWarning(
                    $"[GameplayLifetimeScope] Skipped registration of {typeof(T).Name} — no instance in any loaded scene.");
            }
        }
    }
}
