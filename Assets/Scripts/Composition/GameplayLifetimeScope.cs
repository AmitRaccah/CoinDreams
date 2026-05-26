#nullable enable

using VContainer;
using VContainer.Unity;

namespace Game.Composition
{
    public sealed class GameplayLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            // Gameplay scope: scene-local services. Inherits Persistent's bindings.
            // Set this scope's Parent field to PersistentLifetimeScope in the inspector.
            //
            // Register here in upcoming phases:
            //   - PlayerRuntimeContext (RegisterComponentInHierarchy)
            //   - VillageUpgradeService + catalog
            //   - DrawWorkflowExecutor + presenters (RegisterEntryPoint for IInitializable)
            //   - Cards engine bindings
            //
            // Pattern reference:
            //   builder.RegisterComponentInHierarchy<PlayerRuntimeContext>();
            //   builder.Register<VillageUpgradeService>(Lifetime.Scoped);
            //   builder.RegisterEntryPoint<DrawWorkflowExecutor>();
        }
    }
}
