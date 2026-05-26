namespace Game.Domain.Player
{
    public enum PlayerImpactApplyStatus
    {
        Applied = 0,
        AppliedPartially = 1,
        AppliedNothing = 2,
        DuplicateIgnored = 3,
        Invalid = 4
    }
}
