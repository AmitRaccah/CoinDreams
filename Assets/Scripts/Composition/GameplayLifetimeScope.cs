#nullable enable

using Game.Runtime.Cards;
using Game.Runtime.Cameras;
using Game.Runtime.Scenes;
using Game.Runtime.Steal;
using Game.Runtime.UI.Buildings;
using Game.Runtime.UI.Panels;
using Game.Runtime.UI.Shields;
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
            // Workflow state lives in a plain service (not on the controller
            // MonoBehaviour). Registered AsSelf so the controller mutates it
            // and As<IDrawWorkflowStateReader> so UI bridges (the Feel
            // trigger) subscribe to state-change events without depending on
            // the concrete state machine.
            builder.Register<CardDrawWorkflowStateMachine>(Lifetime.Scoped)
                .As<IDrawWorkflowStateReader>()
                .AsSelf();
            TryRegisterInHierarchy<CardDrawWorkflowController>(builder);
            TryRegisterInHierarchy<DrawHudPresenter>(builder);
            TryRegisterInHierarchy<DrawActionPresenter>(builder);
            TryRegisterDrawCardPresentation(builder);
            TryRegisterInHierarchy<VillageUpgradeRuntime>(builder);
            // 3D doll lives in this scene — subscribes to voodoo signals
            // brokered in the persistent parent scope.
            TryRegisterInHierarchy<Voodoo3DDollPresenter>(builder);
            // Voodoo presenters live on the loaded PF_Doll_Voodo root and
            // subscribe to the same persistent-scope signals.
            TryRegisterInHierarchy<VoodooVictimNamePresenter>(builder);
            TryRegisterInHierarchy<VoodooVictimStolenAmountPresenter>(builder);
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
                PersistentLifetimeScope.InjectAllInScenes<ShieldsHudPresenter>(container);
                // DrawWorkflowFeelTrigger may live in 01_Persistent (alongside
                // the Canvas) but needs IDrawWorkflowStateReader, which is
                // registered in THIS gameplay scope. Injecting from here
                // hands it this scope's container so the resolve succeeds.
                PersistentLifetimeScope.InjectAllInScenes<DrawWorkflowFeelTrigger>(container);
                // VoodooFeelTrigger instances that live in 02_Gameplay
                // (next to Alter_Draw_Game / the doll) miss PersistentLifetimeScope's
                // sweep because their scene isn't loaded yet at that time.
                // Re-running here picks them up. IVoodooSessionStateReader is
                // a persistent-scope service; VContainer resolves parent-scope
                // dependencies transparently.
                PersistentLifetimeScope.InjectAllInScenes<Game.Runtime.Steal.VoodooFeelTrigger>(container);
            });
        }

        private static void TryRegisterInHierarchy<T>(IContainerBuilder builder) where T : Component
        {
            T? instance = Object.FindAnyObjectByType<T>();
            if (instance == null)
            {
                T[] inactiveCandidates = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (inactiveCandidates.Length > 0)
                {
                    instance = inactiveCandidates[0];
                }
            }

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

        private static void TryRegisterDrawCardPresentation(IContainerBuilder builder)
        {
            if (Object.FindAnyObjectByType<DrawCardPresentationPresenter>() != null)
            {
                builder.RegisterComponentInHierarchy<DrawCardPresentationPresenter>()
                    .As<IDrawCardPresentation>()
                    .AsSelf();
                return;
            }

            builder.Register<NullDrawCardPresentation>(Lifetime.Scoped)
                .As<IDrawCardPresentation>();
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
