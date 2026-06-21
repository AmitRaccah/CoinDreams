using System;

namespace Game.Domain.Shields
{
    /// <summary>
    /// Concrete implementation of the shield count + cap. Owns its own
    /// state (no shared reference into the snapshot) — the persistence
    /// layer rebuilds a fresh instance from the snapshot on profile load,
    /// and serialises back via <see cref="GetCurrent"/> / <see cref="GetMax"/>
    /// when writing.
    ///
    /// SRP: only counts + bounded mutation + change event. Knows nothing
    /// about energy refunds, draw effects, or the steal flow — callers
    /// orchestrate those.
    /// </summary>
    public sealed class ShieldService : IShieldService, IReadOnlyShieldService
    {
        public event Action<int, int> ShieldsChanged;

        private int currentShields;
        private int maxShields;

        public ShieldService(int startShields, int maxShields)
        {
            this.maxShields = NormalizeMax(maxShields);

            int clamped = startShields < 0 ? 0 : startShields;
            if (clamped > this.maxShields)
            {
                clamped = this.maxShields;
            }
            this.currentShields = clamped;
        }

        public int GetCurrent()
        {
            return currentShields;
        }

        public int GetMax()
        {
            return maxShields;
        }

        public int TryAdd(int amount)
        {
            if (amount <= 0)
            {
                return 0;
            }

            int space = maxShields - currentShields;
            if (space <= 0)
            {
                return amount;
            }

            int added = amount;
            int overflow = 0;
            if (added > space)
            {
                overflow = added - space;
                added = space;
            }

            currentShields += added;
            NotifyChanged();
            return overflow;
        }

        public bool TryConsume()
        {
            if (currentShields <= 0)
            {
                return false;
            }

            currentShields--;
            NotifyChanged();
            return true;
        }

        private static int NormalizeMax(int value)
        {
            // Zero would make the service useless (no room, every TryAdd
            // is a full overflow). Clamp to 1 minimum — server can still
            // gate access at a higher level if the feature is "off".
            if (value <= 0)
            {
                return 1;
            }
            return value;
        }

        private void NotifyChanged()
        {
            Action<int, int> handler = ShieldsChanged;
            if (handler != null)
            {
                handler(currentShields, maxShields);
            }
        }
    }
}
