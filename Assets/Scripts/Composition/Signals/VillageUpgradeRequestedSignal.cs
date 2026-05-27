namespace Game.Composition.Signals
{
    public readonly struct VillageUpgradeRequestedSignal
    {
        public readonly string BuildingId;
        public readonly int BuildingIndex;
        public readonly bool UseIndex;

        public VillageUpgradeRequestedSignal(string buildingId, int buildingIndex, bool useIndex)
        {
            BuildingId = buildingId ?? string.Empty;
            BuildingIndex = buildingIndex;
            UseIndex = useIndex;
        }
    }
}
