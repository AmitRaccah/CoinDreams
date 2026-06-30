namespace Game.Signals
{
    /// <summary>
    /// Raised when every building in the village reaches max level — the stage
    /// is complete and ready to advance. Carries the stage number that was just
    /// completed (the player's currentStage before advancing) so the
    /// stage-complete UI can show "Stage N cleared".
    ///
    /// Published by VillageUpgradeRuntime; consumed by the stage-complete UI.
    /// </summary>
    public readonly struct StageCompletedSignal
    {
        public readonly int CompletedStage;

        public StageCompletedSignal(int completedStage)
        {
            CompletedStage = completedStage;
        }
    }
}
