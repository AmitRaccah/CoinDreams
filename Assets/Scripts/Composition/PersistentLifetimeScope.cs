#nullable enable

using Game.Composition.Signals;
using Game.Domain.Minigames;
using Game.Domain.Time;
using Game.Infrastructure.Persistence;
using Game.Runtime.Cards;
using Game.Runtime.Player;
using Game.Runtime.UI;
using MessagePipe;
using VContainer;
using VContainer.Unity;

namespace Game.Composition
{
    public sealed class PersistentLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            var messagePipeOptions = builder.RegisterMessagePipe();
            builder.RegisterMessageBroker<DrawRequestedSignal>(messagePipeOptions);
            builder.RegisterMessageBroker<ReturnRequestedSignal>(messagePipeOptions);

            builder.Register<TimeProvider>(Lifetime.Singleton).As<ITimeProvider>();
            builder.Register<UiNavigatorStub>(Lifetime.Singleton).As<IUiNavigator>();
            builder.RegisterInstance(NullMinigameLauncher.Instance).As<IMinigameLauncher>();

            builder.RegisterComponentInHierarchy<PlayerRuntimeContext>();
            builder.RegisterComponentInHierarchy<FirebasePlayerPersistenceRuntime>()
                .AsImplementedInterfaces()
                .AsSelf();
            builder.RegisterComponentInHierarchy<DrawHudPresenter>();
            builder.RegisterComponentInHierarchy<DrawActionPresenter>();

            // UI lives in Persistent scene (per recent scene-split refactor), so the binder
            // is registered here, not in GameplayLifetimeScope.
            builder.RegisterComponentInHierarchy<CardDrawHudInputBinder>();

            // Upcoming phases will add here:
            //   - IPlayerRepository (FirestorePlayerRepository) — blocked on Firebase init flow
            //   - IAppLifecycle (replaces AppLifecycleObserver.Current)
            //   - MessagePipe broker + signals (EnergyChangedSignal, CoinsChangedSignal, ProfileReplacedSignal)
        }
    }
}
