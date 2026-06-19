#nullable enable

using Game.Runtime.Cards;
using Game.Runtime.Cameras;
using Game.Runtime.Scenes;
using Game.Runtime.Steal;
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
            // 3D doll lives in this scene — subscribes to voodoo signals
            // brokered in the persistent parent scope.
            TryRegisterInHierarchy<Voodoo3DDollPresenter>(builder);
            // Victim-name presenter shares the doll's GameObject (same scene),
            // subscribes to the same persistent-scope signals.
            TryRegisterInHierarchy<VoodooVictimNamePresenter>(builder);
            // Draw-mode visibility gate — depends on ICameraViewModeReader
            // (registered above as Scoped) and voodoo session signals from the
            // parent scope. The GameObject itself can live in 01_Persistent, so
            // we use RegisterComponent(instance) — RegisterComponentInHierarchy
            // searches only THIS scope's scene (02_Gameplay) and would throw.
            TryRegisterInstance<DrawModeVisibilityPresenter>(builder);
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

        // Same opt-in pattern as TryRegisterInHierarchy but uses RegisterComponent
        // on the resolved instance so components living in another loaded scene
        // (e.g. 01_Persistent) can still resolve gameplay-scope dependencies.
        private static void TryRegisterInstance<T>(IContainerBuilder builder) where T : Component
        {
            T instance = Object.FindAnyObjectByType<T>();
            if (instance != null)
            {
                builder.RegisterComponent(instance);
            }
            else
            {
                Debug.LogWarning(
                    $"[GameplayLifetimeScope] Skipped registration of {typeof(T).Name} — no instance in any loaded scene.");
            }
        }
    }
}
