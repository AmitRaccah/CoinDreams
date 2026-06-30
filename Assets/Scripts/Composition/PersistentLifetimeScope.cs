#nullable enable

using System;
using Game.Signals;
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
            builder.RegisterMessageBroker<StageCompletedSignal>(messagePipeOptions);
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
                // VoodooFeelTrigger subscribes to IVoodooSessionStateReader,
                // which is the VoodooSessionState service registered in this
                // same scope — resolves cleanly without crossing scope
                // boundaries.
                InjectAllInScenes<Game.Runtime.Steal.VoodooFeelTrigger>(container);
            });

            // Button-click feedbacks are wired directly via UnityEvent →
            // MMF_Player.PlayFeedbacks() in the Inspector. State-driven
            // feedbacks (transitions, gating) go through the two Feel triggers
            // — DrawWorkflowFeelTrigger + VoodooFeelTrigger — registered above.

            builder.Register<TimeProvider>(Lifetime.Singleton).As<ITimeProvider>();
            builder.Register<UiNavigatorStub>(Lifetime.Singleton).As<IUiNavigator>();
            // Card-draw side effects. Each is registered as ICardDrawEffect
            // so the workflow executor can inject IReadOnlyList<ICardDrawEffect>
            // and orchestrate them generically (filter → prepare-in-parallel
            // with the card animation → apply at the end). Adding new card
            // types (attack, bonus, jackpot, ...) requires only a new class
            // + one Register line here — zero changes to the executor.
            builder.Register<StealCardEffect>(Lifetime.Singleton).As<ICardDrawEffect>();

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

            builder.Register<FirebaseAuthService>(Lifetime.Singleton)
                .AsImplementedInterfaces();    // picks up IFirebaseAuthService + IDisposable (releases the Firestore live-sync listener on scope teardown)

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

            // Session state lives in a plain service — the coordinator (sole
            // mutator) injects the concrete type; UI/router consumers depend
            // on IVoodooSessionStateReader. Lifetime.Singleton so the same
            // object survives across scope rebuilds, matching the previous
            // coordinator-owned state's lifecycle.
            builder.Register<Game.Runtime.Steal.VoodooSessionState>(Lifetime.Singleton)
                .As<Game.Domain.Steal.IVoodooSessionStateReader>()
                .AsSelf();

            // Coordinator is required (signal dispatch + phase orchestration).
            // No longer implements IVoodooSessionStateReader — state is the
            // separate service above. Presenters live in the Gameplay scene
            // (Voodoo3DDollPresenter, VoodooVictimNamePresenter); scenes
            // like 0.1_Steal can run without them.
            builder.RegisterComponentInHierarchy<VoodooStealCoordinator>();

            // Voodoo3DDollPresenter is registered in GameplayLifetimeScope — it
            // lives in the 3D-world scene (02_Gameplay), not in the persistent
            // HUD scene. Signals from this parent scope still reach it.
            if (UnityEngine.Object.FindAnyObjectByType<AutoStartVoodooSession>() != null)
            {
                builder.RegisterComponentInHierarchy<AutoStartVoodooSession>();
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

            builder.Register<CloudFunctionsStealClient>(Lifetime.Singleton).As<IVoodooStealClient>();

            // Stage-advance authority lives ONLY in the cloud function; this is
            // the client that calls it. The reset village/stage comes back via
            // LiveSync, so there is no client-side advance engine to duplicate.
            builder.Register<Game.Infrastructure.CloudFunctions.CloudFunctionsStageClient>(Lifetime.Singleton)
                .As<Game.Domain.Stages.IStageAdvanceClient>();

            // Coin-gain presentation gate: withholds the HUD balance bump and
            // the coin-gain Feel chain while a stab animation plays, flushing
            // them when the doll animation completes. Registered here (next to
            // the stab signal brokers it listens to); the HUD + village coin
            // presenters consume it through ICoinPresentationGate.
            builder.Register<Game.Runtime.Steal.VoodooCoinPresentationGate>(Lifetime.Singleton)
                .As<Game.Runtime.Economy.ICoinPresentationGate>();

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
