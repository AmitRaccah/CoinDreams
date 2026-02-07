namespace Game.Cards
{
    public sealed class RewardContext
    {
        private readonly Game.Services.Energy.EnergyService energy;
        private readonly Game.Services.Economy.CurrencyService currency;
        private readonly Game.Services.Cards.DrawModifiersService modifiers;
        private readonly Game.Services.Minigames.IMinigameLauncher minigames;

        private int currentDrawMultiplier;
        private bool scaledResourceRewardApplied;

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
            currentDrawMultiplier = 1;
            scaledResourceRewardApplied = false;
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

        public void BeginDraw()
        {
            modifiers.BeginDraw();
            currentDrawMultiplier = modifiers.GetCurrentDrawMultiplier();
            scaledResourceRewardApplied = false;
        }

        public void EndDraw()
        {
            modifiers.CompleteDraw(scaledResourceRewardApplied);
            currentDrawMultiplier = 1;
            scaledResourceRewardApplied = false;
        }

        public void AddToResource(RewardResourceType resourceType, int baseAmount)
        {
            if (baseAmount == 0)
            {
                return;
            }

            int scaledAmount = ScaleAmount(baseAmount, currentDrawMultiplier);

            if (resourceType == RewardResourceType.Currency)
            {
                currency.Add(scaledAmount);
                scaledResourceRewardApplied = true;
                return;
            }

            if (resourceType == RewardResourceType.Energy)
            {
                energy.Add(scaledAmount);
                scaledResourceRewardApplied = true;
            }
        }

        private static int ScaleAmount(int amount, int multiplier)
        {
            long scaled = (long)amount * multiplier;

            if (scaled > int.MaxValue)
            {
                return int.MaxValue;
            }

            if (scaled < int.MinValue)
            {
                return int.MinValue;
            }

            return (int)scaled;
        }
    }
}
