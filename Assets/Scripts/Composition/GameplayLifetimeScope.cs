#nullable enable

using VContainer;
using VContainer.Unity;

namespace Game.Composition
{
    public sealed class GameplayLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            // Note: CardDrawHudInputBinder is registered in PersistentLifetimeScope because
            // the UI canvas moved to the Persistent scene in the recent scene-split refactor.
            // CardDrawWorkflowController lives here in Gameplay but its registration sits
            // alongside the binder (as a factory) to bridge the scope boundary until
            // MessagePipe signals replace the direct dependency.

            // Upcoming phases will add here:
            //   - PlayerRuntimeContext (RegisterComponentInHierarchy)
            //   - DrawHudPresenter + DrawActionPresenter
            //   - VillageUpgradeRuntime + AuthoritativeVillageUpgradeExecutor
        }
    }
}
