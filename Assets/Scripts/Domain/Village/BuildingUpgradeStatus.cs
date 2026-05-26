namespace Game.Domain.Village
{
    public enum BuildingUpgradeStatus
    {
        Success = 0,
        MaxLevel = 1,
        NotEnoughCurrency = 2,
        InvalidConfig = 3,
        AlreadyInProgress = 4,
        ServiceUnavailable = 5,
        UnexpectedError = 6,
        AlreadyApplied = 7
    }
}
