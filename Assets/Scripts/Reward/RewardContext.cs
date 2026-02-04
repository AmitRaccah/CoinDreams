namespace Game.Cards
{
    public sealed class RewardContext
    {
        public Game.Services.Energy.EnergyService Energy { get; private set; }
        public Game.Services.Economy.CurrencyService Currency { get; private set; }
        public Game.Services.Cards.DrawModifiersService Modifiers { get; private set; }
        public Game.Services.Minigames.IMinigameLauncher Minigames { get; private set; }

        public RewardContext(
            Game.Services.Energy.EnergyService energy,
            Game.Services.Economy.CurrencyService currency,
            Game.Services.Cards.DrawModifiersService modifiers,
            Game.Services.Minigames.IMinigameLauncher minigames)
        {
            Energy = energy;
            Currency = currency;
            Modifiers = modifiers;
            Minigames = minigames;
        }
    }
}