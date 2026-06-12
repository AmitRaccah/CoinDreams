using System;

namespace Game.Domain.Cards
{
    public sealed class RewardContext
    {
        private readonly Game.Domain.Energy.IEnergyService energy;
        private readonly Game.Domain.Economy.ICurrencyWallet currency;
        private readonly Game.Domain.Cards.DrawModifiersService modifiers;
        private readonly Game.Domain.Minigames.IMinigameLauncher minigames;
        private readonly int drawMultiplier;

        public RewardContext(
            Game.Domain.Energy.IEnergyService energy,
            Game.Domain.Economy.ICurrencyWallet currency,
            Game.Domain.Cards.DrawModifiersService modifiers,
            Game.Domain.Minigames.IMinigameLauncher minigames)
        {
            if (modifiers == null)
            {
                throw new ArgumentNullException(nameof(modifiers));
            }

            this.energy = energy;
            this.currency = currency;
            this.modifiers = modifiers;
            this.minigames = minigames;
            drawMultiplier = modifiers.GetCurrentDrawMultiplier();
        }

        public Game.Domain.Energy.IEnergyService Energy
        {
            get { return energy; }
        }

        public Game.Domain.Economy.ICurrencyWallet Currency
        {
            get { return currency; }
        }

        public Game.Domain.Cards.DrawModifiersService Modifiers
        {
            get { return modifiers; }
        }

        public Game.Domain.Minigames.IMinigameLauncher Minigames
        {
            get { return minigames; }
        }

        public void AddToResource(RewardResourceType resourceType, int baseAmount)
        {
            if (baseAmount == 0)
            {
                return;
            }

            int scaledAmount = ScaleAmount(baseAmount, drawMultiplier);

            if (resourceType == RewardResourceType.Currency)
            {
                currency.Add(scaledAmount);
                return;
            }

            if (resourceType == RewardResourceType.Energy)
            {
                energy.Add(scaledAmount);
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
