#nullable enable

using Game.Runtime.Cards;
using Game.Runtime.Cameras;
using Game.Runtime.Scenes;
using Game.Runtime.Steal;
using Game.Runtime.UI.Buildings;
using Game.Runtime.UI.Panels;
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

            // Camera tags live in this scope because ICameraViewModeReader
            // does. The UiContextService it writes into is resolved from the
            // persistent parent scope, so binders and the panel/steal
            // publishers all see the camera-{mode} flips immediately.
            builder.Register<Game.Runtime.UI.Context.Publishers.CameraTagsPublisher>(Lifetime.Singleton)
                .AsImplementedInterfaces();

            // Draw-workflow tags also live in Gameplay because the controller
            // that exposes IDrawWorkflowStateReader is in this scene. Fires
            // the draw-engaged tag on the same frame the workflow leaves
            // Idle — earlier than camera-board, which only flips after the
            // transition finishes animating.
            builder.Register<Game.Runtime.UI.Context.Publishers.DrawWorkflowTagsPublisher>(Lifetime.Singleton)
                .AsImplementedInterfaces();

            // Guard each registration: VContainer's RegisterComponentInHierarchy throws if no
            // matching component exists in the scope's scene. Skipping a missing component lets
            // the rest of the container build instead of taking down the whole scope.
            TryRegisterInHierarchy<MapOrbitCameraController>(builder);
            // Inline registration (instead of TryRegisterInHierarchy) so the
            // controller is exposed as IDrawWorkflowStateReader too — that's
            // what DrawWorkflowTagsPublisher injects to listen for state.
            if (Object.FindAnyObjectByType<CardDrawWorkflowController>() != null)
            {
                builder.RegisterComponentInHierarchy<CardDrawWorkflowController>()
                    .AsImplementedInterfaces()
                    .AsSelf();
            }
            else
            {
                Debug.LogWarning(
                    "[GameplayLifetimeScope] Skipped registration of CardDrawWorkflowController — no instance in any loaded scene.");
            }
            TryRegisterInHierarchy<DrawHudPresenter>(builder);
            TryRegisterInHierarchy<DrawActionPresenter>(builder);
            TryRegisterInHierarchy<VillageUpgradeRuntime>(builder);
            // 3D doll lives in this scene — subscribes to voodoo signals
            // brokered in the persistent parent scope.
            TryRegisterInHierarchy<Voodoo3DDollPresenter>(builder);
            // Victim-name presenter shares the doll's GameObject (same scene),
            // subscribes to the same persistent-scope signals.
            TryRegisterInHierarchy<VoodooVictimNamePresenter>(builder);
            // Buildings panel / Presenter / panel buttons are NOT registered
            // as services — they're "drop-on" MonoBehaviours whose [Inject]
            // fields just need filling. PersistentLifetimeScope already runs
            // the same callback; we re-run it here so anything that lives in
            // 02_Gameplay (only loaded AFTER PersistentLifetimeScope.Configure)
            // also gets injected. Calling Inject() twice on the same instance
            // is idempotent — it just re-fills the same fields.
            builder.RegisterBuildCallback(container =>
            {
                PersistentLifetimeScope.InjectAllInScenes<PanelOpenButton>(container);
                PersistentLifetimeScope.InjectAllInScenes<PanelCloseButton>(container);
                PersistentLifetimeScope.InjectAllInScenes<BuildingsPanel>(container);
                PersistentLifetimeScope.InjectAllInScenes<BuildingsPanelPresenter>(container);
                PersistentLifetimeScope.InjectAllInScenes<Game.Runtime.UI.Context.UiTaggedVisibility>(container);
            });
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
