#nullable enable

using System;
using Game.Composition.Signals;
using Game.Domain.Minigames;
using Game.Domain.Time;
using Game.Infrastructure.Persistence;
using Game.Runtime.Cards;
using Game.Runtime.Lifecycle;
using Game.Runtime.Player;
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

        protected override void Configure(IContainerBuilder builder)
        {
            var messagePipeOptions = builder.RegisterMessagePipe();
            builder.RegisterMessageBroker<DrawRequestedSignal>(messagePipeOptions);
            builder.RegisterMessageBroker<ReturnRequestedSignal>(messagePipeOptions);
            builder.RegisterMessageBroker<VillageUpgradeRequestedSignal>(messagePipeOptions);

            builder.Register<TimeProvider>(Lifetime.Singleton).As<ITimeProvider>();
            builder.Register<UiNavigatorStub>(Lifetime.Singleton).As<IUiNavigator>();
            builder.RegisterInstance(NullMinigameLauncher.Instance).As<IMinigameLauncher>();

            // ===== Persistence (Phase 2 split) =====
            if (persistenceSettings == null)
            {
                throw new InvalidOperationException(
                    "PersistentLifetimeScope: PersistenceSettings asset is not assigned. "
                    + "Drag a PersistenceSettings ScriptableObject into the inspector slot.");
            }
            builder.RegisterInstance(persistenceSettings);

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

            builder.RegisterComponentInHierarchy<PlayerRuntimeContext>();
            builder.RegisterComponentInHierarchy<FirebasePersistenceBootstrap>();
            builder.RegisterComponentInHierarchy<AutosaveDriver>();
            builder.RegisterComponentInHierarchy<AppLifecycleObserver>()
                .AsImplementedInterfaces()
                .AsSelf();

            // UI binder lives in Persistent scene (per recent scene-split refactor); the Draw
            // gameplay presenters live in the Gameplay scene and are registered there instead.
            builder.RegisterComponentInHierarchy<CardDrawHudInputBinder>();

            // HUD widget references live on the Canvas in the Persistent scene. Registering them
            // here lets the Gameplay-scope DrawHudPresenter resolve them through the parent scope
            // and avoids broken cross-scene SerializeField references.
            builder.RegisterComponentInHierarchy<CardDrawHudReferences>();

            // Upcoming phases will add here:
            //   - IPlayerRepository (FirestorePlayerRepository) — blocked on Firebase init flow
            //   - MessagePipe broker + signals (EnergyChangedSignal, CoinsChangedSignal, ProfileReplacedSignal)
        }
    }
}
