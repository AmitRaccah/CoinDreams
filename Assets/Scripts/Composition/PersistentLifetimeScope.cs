#nullable enable

using System;
using Game.Composition.Signals;
using Game.Config.Cards;
using Game.Domain.Steal;
using Game.Domain.Time;
using Game.Infrastructure.CloudFunctions;
using Game.Infrastructure.Persistence;
using Game.Runtime.Cards;
using Game.Runtime.Lifecycle;
using Game.Runtime.Player;
using Game.Runtime.Steal;
using Game.Runtime.Steal.Phases;
using Game.Runtime.UI;
using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Composition
{
    public sealed class PersistentLifetimeScope : LifetimeScope
    {
        [SerializeField] private PersistenceSettings? persistenceSettings;
        [SerializeField] private CardDrawConfigSO? cardDrawConfig;

        protected override void Configure(IContainerBuilder builder)
        {
            var messagePipeOptions = builder.RegisterMessagePipe();
            builder.RegisterMessageBroker<DrawButtonClickedSignal>(messagePipeOptions);
            builder.RegisterMessageBroker<DrawRequestedSignal>(messagePipeOptions);
            builder.RegisterMessageBroker<ReturnRequestedSignal>(messagePipeOptions);
            builder.RegisterMessageBroker<VillageUpgradeRequestedSignal>(messagePipeOptions);
            builder.RegisterMessageBroker<StealCardTriggeredSignal>(messagePipeOptions);
            builder.RegisterMessageBroker<VoodooSessionStartedSignal>(messagePipeOptions);
            builder.RegisterMessageBroker<VoodooSessionEndedSignal>(messagePipeOptions);
            builder.RegisterMessageBroker<VoodooStabRequestedSignal>(messagePipeOptions);
            builder.RegisterMessageBroker<VoodooStabResolvedSignal>(messagePipeOptions);
            builder.RegisterMessageBroker<VoodooStabAnimationCompletedSignal>(messagePipeOptions);
            builder.RegisterMessageBroker<MultiplierChangeRequestedSignal>(messagePipeOptions);
            builder.RegisterMessageBroker<PanelOpenRequestedSignal>(messagePipeOptions);
            builder.RegisterMessageBroker<PanelCloseRequestedSignal>(messagePipeOptions);
            builder.RegisterMessageBroker<PanelVisibilityChangedSignal>(messagePipeOptions);

            // PanelNavigator is the only opinion on "which panel is current".
            // AsImplementedInterfaces picks up IInitializable (subscribes to
            // signals at container build) and IDisposable (drops subs on shutdown).
            builder.Register<Game.Runtime.UI.Panels.PanelNavigator>(Lifetime.Singleton)
                .AsImplementedInterfaces()
                .AsSelf();

            // Panel-system components live across scenes and can have N
            // instances per type (many side-rail buttons, many close-X
            // buttons, one BuildingsPanel, one Presenter). VContainer's
            // RegisterComponentInHierarchy only handles one-per-scope, so
            // we inject all matching components manually at container build
            // using FindObjectsByType (walks every loaded scene + disabled
            // GameObjects). GameplayLifetimeScope re-runs the same callback
            // for scenes that load after PersistentLifetimeScope.Configure.
            builder.RegisterBuildCallback(container =>
            {
                // Inject ONLY components whose [Inject] dependencies live in
                // this persistent scope. BuildingsPanelPresenter depends on
                // VillageUpgradeRuntime which is registered in Gameplay scope
                // — injecting it here while 02_Gameplay hasn't loaded yet
                // would throw a VContainerException and tear down bootstrap.
                // The Presenter is injected from GameplayLifetimeScope instead.
                InjectAllInScenes<Game.Runtime.UI.Panels.PanelOpenButton>(container);
                InjectAllInScenes<Game.Runtime.UI.Panels.PanelCloseButton>(container);
                InjectAllInScenes<Game.Runtime.UI.Buildings.BuildingsPanel>(container);
                InjectAllInScenes<Game.Runtime.UI.Shields.ShieldsHudPresenter>(container);
            });

            builder.Register<TimeProvider>(Lifetime.Singleton).As<ITimeProvider>();
            builder.Register<UiNavigatorStub>(Lifetime.Singleton).As<IUiNavigator>();
            builder.Register<VoodooStealCardLauncher>(Lifetime.Singleton).As<IStealCardLauncher>();

            // ===== Persistence (Phase 2 split) =====
            if (persistenceSettings == null)
            {
                throw new InvalidOperationException(
                    "PersistentLifetimeScope: PersistenceSettings asset is not assigned. "
                    + "Drag a PersistenceSettings ScriptableObject into the inspector slot.");
            }
            builder.RegisterInstance(persistenceSettings);

            if (cardDrawConfig == null)
            {
                throw new InvalidOperationException(
                    "PersistentLifetimeScope: CardDrawConfigSO asset is not assigned. "
                    + "Drag a CardDrawConfigSO ScriptableObject into the inspector slot.");
            }
            builder.RegisterInstance(cardDrawConfig);

            builder.Register<AutosaveScheduler>(
                _ => new AutosaveScheduler(persistenceSettings.AutosaveIntervalSeconds),
                Lifetime.Singleton);

            builder.Register<FirebaseAuthService>(Lifetime.Singleton).As<IFirebaseAuthService>();

            builder.Register<LocalSnapshotCache>(
                _ => new LocalSnapshotCache(
                    persistenceSettings.UseLocalCache,
                    persistenceSettings.LocalCacheFileName),
                Lifetime.Singleton).As<ILocalSnapshotCache>();

            builder.Register<PlayerSnapshotService>(Lifetime.Singleton)
                .AsImplementedInterfaces();    // picks up IPlayerSnapshotService + IInitializable + IDisposable

            builder.Register<AuthoritativeActionsService>(Lifetime.Singleton)
                .AsImplementedInterfaces();    // picks up IAuthoritativeDrawService, IAuthoritativeVillageUpgradeService, IAuthoritativeActionsService

            builder.RegisterComponentInHierarchy<PlayerRuntimeContext>()
                .AsImplementedInterfaces()
                .AsSelf();
            builder.RegisterComponentInHierarchy<FirebasePersistenceBootstrap>();
            builder.RegisterComponentInHierarchy<AutosaveDriver>();
            builder.RegisterComponentInHierarchy<AppLifecycleObserver>()
                .AsImplementedInterfaces()
                .AsSelf();

            // UI binder lives in Persistent scene (per recent scene-split refactor); the Draw
            // gameplay presenters live in the Gameplay scene and are registered there instead.
            builder.RegisterComponentInHierarchy<CardDrawHudInputBinder>();

            // Multiplier button is opt-in — only register if a binder exists in the scene
            // (otherwise startup would throw before the user has time to wire the UI).
            if (UnityEngine.Object.FindAnyObjectByType<DrawMultiplierBinder>() != null)
            {
                builder.RegisterComponentInHierarchy<DrawMultiplierBinder>();
            }

            // HUD widget references live on the Canvas in the Persistent scene. Registering them
            // here lets the Gameplay-scope DrawHudPresenter resolve them through the parent scope
            // and avoids broken cross-scene SerializeField references.
            builder.RegisterComponentInHierarchy<CardDrawHudReferences>();

            // ===== Voodoo steal feature =====
            // Three timelines own the per-phase sequencing (server calls +
            // signal publishes today, cinematics tomorrow). The coordinator
            // is now a thin state holder that dispatches incoming signals to
            // the right phase. Phases are stateless services so they
            // register as plain Singletons.
            builder.Register<VoodooEntryPhase>(Lifetime.Singleton);
            builder.Register<VoodooActionPhase>(Lifetime.Singleton);
            builder.Register<VoodooExitPhase>(Lifetime.Singleton);

            // Coordinator is required (state + dispatch). Presenters live in
            // the Gameplay scene (Voodoo3DDollPresenter, VoodooVictimName-
            // Presenter) — scenes like 0.1_Steal can run without them (the
            // coordinator still runs the timelines, server still moves coins).
            builder.RegisterComponentInHierarchy<VoodooStealCoordinator>()
                .AsImplementedInterfaces()
                .AsSelf();

            // Voodoo3DDollPresenter is registered in GameplayLifetimeScope — it
            // lives in the 3D-world scene (02_Gameplay), not in the persistent
            // HUD scene. Signals from this parent scope still reach it.
            if (UnityEngine.Object.FindAnyObjectByType<AutoStartVoodooSession>() != null)
            {
                builder.RegisterComponentInHierarchy<AutoStartVoodooSession>();
            }
            if (UnityEngine.Object.FindAnyObjectByType<VoodooStabHudSync>() != null)
            {
                builder.RegisterComponentInHierarchy<VoodooStabHudSync>();
            }
            // VoodooVictimNamePresenter is registered in GameplayLifetimeScope —
            // it lives on VoodooDoll3D in 02_Gameplay, so RegisterComponent-
            // InHierarchy from this persistent scope wouldn't find it
            // (VContainer scopes only see their own scene).

            // Router is opt-in — scenes without the mediator (legacy/test scenes) still
            // resolve cleanly because the binder publishes the click signal unconditionally.
            if (UnityEngine.Object.FindAnyObjectByType<DrawButtonRouter>() != null)
            {
                builder.RegisterComponentInHierarchy<DrawButtonRouter>();
            }

            // Interactability binder mirrors IsTransitioning onto Button.interactable so
            // clicks are dropped at Unity's EventSystem layer (not just at the router).
            // Same opt-in pattern as the router — scenes that don't wire it still work.
            if (UnityEngine.Object.FindAnyObjectByType<DrawButtonInteractabilityBinder>() != null)
            {
                builder.RegisterComponentInHierarchy<DrawButtonInteractabilityBinder>();
            }

            builder.Register<CloudFunctionsStealClient>(Lifetime.Singleton).As<IVoodooStealClient>();

            // Upcoming phases will add here:
            //   - IPlayerRepository (FirestorePlayerRepository) — blocked on Firebase init flow
            //   - MessagePipe broker + signals (EnergyChangedSignal, CoinsChangedSignal, ProfileReplacedSignal)
        }

        // Walks every loaded scene (including disabled GameObjects), finds
        // every Component of type T, and runs the container's [Inject] pass
        // on each. Used for "drop-on" components like the panel buttons:
        // they don't need to be registered as services (nobody injects them
        // anywhere), they just need their own [Inject] fields filled.
        // GameplayLifetimeScope shares this helper to re-run injection for
        // scenes that load after the persistent container is built.
        internal static void InjectAllInScenes<T>(VContainer.IObjectResolver container) where T : Component
        {
            T[] instances = UnityEngine.Object.FindObjectsByType<T>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < instances.Length; i++)
            {
                container.Inject(instances[i]);
            }
        }
    }
}
