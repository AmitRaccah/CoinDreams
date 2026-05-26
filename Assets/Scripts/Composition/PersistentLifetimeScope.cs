#nullable enable

using Game.Domain.Minigames;
using Game.Domain.Time;
using Game.Infrastructure.Persistence;
using Game.Runtime.Cards;
using Game.Runtime.UI;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Composition
{
    public sealed class PersistentLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<TimeProvider>(Lifetime.Singleton).As<ITimeProvider>();
            builder.Register<UiNavigatorStub>(Lifetime.Singleton).As<IUiNavigator>();
            builder.RegisterInstance(NullMinigameLauncher.Instance).As<IMinigameLauncher>();

            builder.RegisterComponentInHierarchy<FirebasePlayerPersistenceRuntime>();

            // UI lives in Persistent scene (per recent scene-split refactor), so the binder
            // is registered here, not in GameplayLifetimeScope.
            builder.RegisterComponentInHierarchy<CardDrawHudInputBinder>();

            // ICardDrawWorkflowCommands lives in the Gameplay scene. Persistent scope can't
            // see Gameplay-scene hierarchy at build time, so resolve lazily via scene search.
            // TODO Phase 1.4: replace with MessagePipe pub/sub (binder publishes
            // DrawRequestedSignal / ReturnRequestedSignal, controller subscribes) once the
            // signals are defined. Drops the FindAnyObjectByType call entirely.
            builder.Register<ICardDrawWorkflowCommands>(
                _ => Object.FindAnyObjectByType<CardDrawWorkflowController>(),
                Lifetime.Singleton);

            // Upcoming phases will add here:
            //   - IPlayerRepository (FirestorePlayerRepository) — blocked on Firebase init flow
            //   - IAppLifecycle (replaces AppLifecycleObserver.Current)
            //   - MessagePipe broker + signals (EnergyChangedSignal, CoinsChangedSignal, ProfileReplacedSignal)
        }
    }
}
