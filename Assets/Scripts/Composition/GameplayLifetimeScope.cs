#nullable enable

using Game.Runtime.Cards;
using Game.Runtime.Village;
using VContainer;
using VContainer.Unity;

namespace Game.Composition
{
    public sealed class GameplayLifetimeScope : LifetimeScope
    {
        protected override void Awake()
        {
            if (parentReference.Type == null)
            {
                parentReference = ParentReference.Create<PersistentLifetimeScope>();
            }
            base.Awake();
        }

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponentInHierarchy<CardDrawWorkflowController>();
            builder.RegisterComponentInHierarchy<DrawHudPresenter>();
            builder.RegisterComponentInHierarchy<DrawActionPresenter>();
            builder.RegisterComponentInHierarchy<VillageUpgradeRuntime>();
        }
    }
}
