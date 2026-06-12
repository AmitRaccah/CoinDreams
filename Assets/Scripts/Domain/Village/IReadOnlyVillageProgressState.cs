using System;

namespace Game.Domain.Village
{
    public interface IReadOnlyVillageProgressState
    {
        event Action Changed;

        int BuildingCount { get; }

        bool TryGetLevel(int index, out int level);
        int GetLevelOrDefault(int index);
    }
}
