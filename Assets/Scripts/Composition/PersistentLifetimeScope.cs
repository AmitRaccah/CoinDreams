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

            // UI binder lives in Persistent scene (per recent scene-split refactor); the Draw
            // gameplay presenters live in the Gameplay scene and are registered there instead.
            builder.RegisterComponentInHierarchy<CardDrawHudInputBinder>();

            // HUD widget references live on the Canvas in the Persistent scene. Registering them
            // here lets the Gameplay-scope DrawHudPresenter resolve them through the parent scope
            // and avoids broken cross-scene SerializeField references.
            builder.RegisterComponentInHierarchy<CardDrawHudReferences>();

            // Upcoming phases will add here:
            //   - IPlayerRepository (FirestorePlayerRepository) — blocked on Firebase init flow
            //   - IAppLifecycle (replaces AppLifecycleObserver.Current)
            //   - MessagePipe broker + signals (EnergyChangedSignal, CoinsChangedSignal, ProfileReplacedSignal)
        }
    }
}
