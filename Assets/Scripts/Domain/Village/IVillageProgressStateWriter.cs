namespace Game.Domain.Village
{
    /// <summary>
    /// Mutator surface for village progress state. Extends the read-only view with
    /// the level-set operations needed by upgrade services.
    /// </summary>
    public interface IVillageProgressStateWriter : IReadOnlyVillageProgressState
    {
        bool CanSetLevel(int index, int level);
        bool TrySetLevel(int index, int level);
    }
}
