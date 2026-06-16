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
            builder.RegisterMessageBroker<DrawRequestedSignal>(messagePipeOptions);
            builder.RegisterMessageBroker<ReturnRequestedSignal>(messagePipeOptions);
            builder.RegisterMessageBroker<VillageUpgradeRequestedSignal>(messagePipeOptions);
            builder.RegisterMessageBroker<StealCardTriggeredSignal>(messagePipeOptions);
            builder.RegisterMessageBroker<VoodooSessionStartedSignal>(messagePipeOptions);
            builder.RegisterMessageBroker<VoodooSessionEndedSignal>(messagePipeOptions);
            builder.RegisterMessageBroker<VoodooStabRequestedSignal>(messagePipeOptions);
            builder.RegisterMessageBroker<VoodooStabResolvedSignal>(messagePipeOptions);
            builder.RegisterMessageBroker<MultiplierChangeRequestedSignal>(messagePipeOptions);

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
            // Coordinator owns the session lifecycle. Presenters subscribe to signals.
            // The CloudFunctions client implementation is registered only once the Firebase
            // Functions SDK is imported — see TODO at the end of this method.
            builder.RegisterComponentInHierarchy<VoodooStealCoordinator>();
            builder.RegisterComponentInHierarchy<VoodooStabInputBinder>();
            builder.RegisterComponentInHierarchy<VoodooDollPresenter>();
            builder.RegisterComponentInHierarchy<VictimPedestalPresenter>();
            builder.RegisterComponentInHierarchy<CenterStageModeController>();

            builder.Register<CloudFunctionsStealClient>(Lifetime.Singleton).As<IVoodooStealClient>();

            // Upcoming phases will add here:
            //   - IPlayerRepository (FirestorePlayerRepository) — blocked on Firebase init flow
            //   - MessagePipe broker + signals (EnergyChangedSignal, CoinsChangedSignal, ProfileReplacedSignal)
        }
    }
}
