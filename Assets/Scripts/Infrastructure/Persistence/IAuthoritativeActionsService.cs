#nullable enable

using Game.Domain.Cards;
using Game.Domain.Village;

namespace Game.Infrastructure.Persistence
{
    public interface IAuthoritativeActionsService
        : IAuthoritativeDrawService, IAuthoritativeVillageUpgradeService
    {
    }
}
