namespace Game.Runtime.Village
{
    internal interface IBuildingVisualApplier
    {
        bool IsValid { get; }
        int MaxLevel { get; }
        void ApplyLevel(int level);
        void Dispose();
    }
}
