using System;

namespace Game.Domain.Cards
{
    public sealed class RewardContext
    {
        private readonly Game.Domain.Energy.IEnergyService energy;
        private readonly Game.Domain.Economy.ICurrencyWallet currency;
        private readonly Game.Domain.Shields.IShieldService? shields;
        private readonly Game.Domain.Cards.DrawModifiersService modifiers;
        private readonly Game.Domain.Steal.IStealCardLauncher stealCardLauncher;
        private readonly int drawMultiplier;

        public RewardContext(
            Game.Domain.Energy.IEnergyService energy,
            Game.Domain.Economy.ICurrencyWallet currency,
            Game.Domain.Shields.IShieldService? shields,
            Game.Domain.Cards.DrawModifiersService modifiers,
            Game.Domain.Steal.IStealCardLauncher stealCardLauncher)
        {
            if (modifiers == null)
            {
                throw new ArgumentNullException(nameof(modifiers));
            }

            this.energy = energy;
            this.currency = currency;
            this.shields = shields;
            this.modifiers = modifiers;
            this.stealCardLauncher = stealCardLauncher;
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

        public Game.Domain.Steal.IStealCardLauncher StealCardLauncher
        {
            get { return stealCardLauncher; }
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

        // Mirrors the server-side AddShields branch in executeDraw.ts: fill
        // to the cap, refund the overflow as energy. The user-facing rule is
        // "you never lose a draw outcome to the cap" — at full shields the
        // entire requested amount comes back as energy, so a Shield drawn at
        // multiplier M nets +0 energy (cost M, refund M) instead of -M.
        public void AddShields(int baseAmount)
        {
            if (baseAmount == 0 || shields == null) return;
            int requested = ScaleAmount(baseAmount, drawMultiplier);
            if (requested <= 0) return;

            // IShieldService.TryAdd returns the OVERFLOW (interface contract),
            // not the amount that landed — full-shield case returns `amount`
            // unchanged. Treating that return as "added" would silently swallow
            // the refund and the player loses the energy they're owed.
            int overflow = shields.TryAdd(requested);

            if (overflow > 0 && energy != null)
            {
                // Refund the FULL overflow regardless of current energy. The
                // EnergyService allows over-cap state by design (the UI's
                // "+X" overflow indicator), so we don't clamp to headroom —
                // the player paid the draw cost and is owed the matching
                // refund whether they were under, at, or over the cap.
                energy.Add(overflow);
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
