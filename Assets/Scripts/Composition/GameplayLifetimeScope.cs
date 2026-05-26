#nullable enable

using Game.Runtime.Cards;
using VContainer;
using VContainer.Unity;

namespace Game.Composition
{
    public sealed class GameplayLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            // CardDrawWorkflowController is the live ICardDrawWorkflowCommands implementation.
            builder.RegisterComponentInHierarchy<CardDrawWorkflowController>()
                .As<ICardDrawWorkflowCommands>();

            // CardDrawHudInputBinder receives [Inject] members via this registration.
            builder.RegisterComponentInHierarchy<CardDrawHudInputBinder>();

            // Upcoming phases will add here:
            //   - PlayerRuntimeContext (RegisterComponentInHierarchy)
            //   - DrawHudPresenter + DrawActionPresenter
            //   - VillageUpgradeRuntime + AuthoritativeVillageUpgradeExecutor
            //   - FirebasePlayerPersistenceRuntime re-exposure as
            //     IAuthoritativeDrawService + IAuthoritativeVillageUpgradeService
        }
    }
}
