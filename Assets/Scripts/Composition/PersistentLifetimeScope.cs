#nullable enable

using VContainer;
using VContainer.Unity;

namespace Game.Composition
{
    public sealed class PersistentLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            // Persistent scope: services that outlive scene reloads.
            // Register here in upcoming phases:
            //   - ITimeProvider (Game.Domain.Time.TimeProvider)
            //   - IPlayerRepository (FirestorePlayerRepository)
            //   - IAppLifecycle (replaces AppLifecycleObserver.Current)
            //   - MessagePipe broker + signal registrations
            //
            // Pattern reference:
            //   builder.Register<TimeProvider>(Lifetime.Singleton).As<ITimeProvider>();
            //   builder.RegisterEntryPoint<FirebaseBootstrap>();
            //   builder.RegisterMessagePipe(options);
            //   builder.RegisterMessageBroker<EnergyChangedSignal>(options);
        }
    }
}
