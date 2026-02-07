namespace Game.Cards
{
    public sealed class RewardContext
    {
        private readonly Game.Services.Energy.EnergyService energy;
        private readonly Game.Services.Economy.CurrencyService currency;
        private readonly Game.Services.Cards.DrawModifiersService modifiers;
        private readonly Game.Services.Minigames.IMinigameLauncher minigames;

        public RewardContext(
            Game.Services.Energy.EnergyService energy,
            Game.Services.Economy.CurrencyService currency,
            Game.Services.Cards.DrawModifiersService modifiers,
            Game.Services.Minigames.IMinigameLauncher minigames)
        {
            this.energy = energy;
            this.currency = currency;
            this.modifiers = modifiers;
            this.minigames = minigames;
        }

        public Game.Services.Energy.EnergyService Energy
        {
            get { return energy; }
        }

        public Game.Services.Economy.CurrencyService Currency
        {
            get { return currency; }
        }

        public Game.Services.Cards.DrawModifiersService Modifiers
        {
            get { return modifiers; }
        }

        public Game.Services.Minigames.IMinigameLauncher Minigames
        {
            get { return minigames; }
        }
    }
}
