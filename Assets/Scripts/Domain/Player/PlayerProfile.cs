using System;
using System.Collections.Generic;
using Game.Domain.Economy;
using Game.Domain.Energy;
using Game.Domain.Time;
using Game.Domain.Village;

namespace Game.Domain.Player
{
    public sealed class PlayerProfile
    {
        private readonly string playerId;
        private readonly CurrencyService currency;
        private readonly EnergyService energy;
        private readonly VillageProgressState village;
        private readonly HashSet<string> processedImpactIds;
        private int revision;

        public PlayerProfile(
            string playerId,
            CurrencyService currency,
            EnergyService energy,
            VillageProgressState village,
            int revision,
            IEnumerable<string> seedProcessedImpactIds)
        {
            if (currency == null)
            {
                throw new ArgumentNullException("currency");
            }

            if (energy == null)
            {
                throw new ArgumentNullException("energy");
            }

            if (village == null)
            {
                throw new ArgumentNullException("village");
            }

            this.playerId = NormalizePlayerId(playerId);
            this.currency = currency;
            this.energy = energy;
            this.village = village;
            this.revision = revision < 0 ? 0 : revision;
            processedImpactIds = new HashSet<string>(StringComparer.Ordinal);
            SeedProcessedImpactIds(seedProcessedImpactIds);
        }

        public string PlayerId
        {
            get { return playerId; }
        }

        public CurrencyService Currency
        {
            get { return currency; }
        }

        public EnergyService Energy
        {
            get { return energy; }
        }

        public VillageProgressState Village
        {
            get { return village; }
        }

        public int Revision
        {
            get { return revision; }
        }

        public void EnsureVillageCapacity(int buildingCount)
        {
            village.EnsureCapacity(buildingCount);
        }

        public bool HasProcessedImpact(string impactId)
        {
            if (string.IsNullOrWhiteSpace(impactId))
            {
                return false;
            }

            return processedImpactIds.Contains(impactId.Trim());
        }

        public PlayerImpactApplyResult ApplyExternalImpact(PlayerImpact impact)
        {
            if (impact == null)
            {
                return PlayerImpactApplyResult.Invalid(
                    string.Empty,
                    PlayerImpactType.CoinsStolen,
                    0,
                    "Impact is null.");
            }

            if (string.IsNullOrWhiteSpace(impact.impactId))
            {
                return PlayerImpactApplyResult.Invalid(
                    string.Empty,
                    impact.impactType,
                    impact.amount,
                    "ImpactId is required.");
            }

            string impactId = impact.impactId.Trim();
            if (processedImpactIds.Contains(impactId))
            {
                return PlayerImpactApplyResult.Duplicate(impactId, impact.impactType);
            }

            if (impact.amount <= 0)
            {
                return PlayerImpactApplyResult.Invalid(
                    impactId,
                    impact.impactType,
                    impact.amount,
                    "Impact amount must be greater than zero.");
            }

            int requestedAmount = impact.amount;
            int appliedAmount = 0;
            int coinsDelta = 0;
            int energyDelta = 0;

            if (impact.impactType == PlayerImpactType.CoinsGranted)
            {
                int beforeCoins = currency.GetCoins();
                currency.Add(requestedAmount);
                int afterCoins = currency.GetCoins();
                appliedAmount = afterCoins - beforeCoins;
                coinsDelta = appliedAmount;
            }
            else if (impact.impactType == PlayerImpactType.CoinsStolen)
            {
                appliedAmount = SpendCoinsUpTo(requestedAmount);
                coinsDelta = -appliedAmount;
            }
            else if (impact.impactType == PlayerImpactType.EnergyGranted)
            {
                int beforeEnergy = energy.GetCurrent();
                energy.Add(requestedAmount);
                int afterEnergy = energy.GetCurrent();
                appliedAmount = afterEnergy - beforeEnergy;
                energyDelta = appliedAmount;
            }
            else if (impact.impactType == PlayerImpactType.EnergyRemoved)
            {
                appliedAmount = SpendEnergyUpTo(requestedAmount);
                energyDelta = -appliedAmount;
            }
            else
            {
                return PlayerImpactApplyResult.Invalid(
                    impactId,
                    impact.impactType,
                    requestedAmount,
                    "Unsupported impact type.");
            }

            processedImpactIds.Add(impactId);
            revision++;

            bool isPartial = appliedAmount < requestedAmount;
            return PlayerImpactApplyResult.Applied(
                impactId,
                impact.impactType,
                requestedAmount,
                appliedAmount,
                coinsDelta,
                energyDelta,
                isPartial);
        }

        public PlayerProfileSnapshot CreateSnapshot()
        {
            energy.ApplyRegen();

            PlayerProfileSnapshot snapshot = new PlayerProfileSnapshot();
            snapshot.playerId = playerId;
            snapshot.revision = revision;
            snapshot.coins = currency.GetCoins();
            snapshot.currentEnergy = energy.GetCurrent();
            snapshot.regenMaxEnergy = energy.GetMax();
            snapshot.storageMaxEnergy = energy.GetStorageMax();
            snapshot.regenIntervalSeconds = energy.GetRegenIntervalSeconds();
            snapshot.lastRegenUtcTicks = energy.GetLastRegenTicks();
            snapshot.villageLevels = village.GetLevelsSnapshot();
            snapshot.processedImpactIds = CreateProcessedImpactIdsSnapshot(processedImpactIds);
            return snapshot;
        }

        public static PlayerProfile FromSnapshot(PlayerProfileSnapshot snapshot, ITimeProvider timeProvider)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException("snapshot");
            }

            if (timeProvider == null)
            {
                throw new ArgumentNullException("timeProvider");
            }

            CurrencyService currency = new CurrencyService();
            if (snapshot.coins > 0)
            {
                currency.Add(snapshot.coins);
            }

            EnergyService energy = new EnergyService(
                timeProvider,
                snapshot.currentEnergy,
                snapshot.regenMaxEnergy,
                snapshot.storageMaxEnergy,
                snapshot.regenIntervalSeconds,
                snapshot.lastRegenUtcTicks);

            VillageProgressState village = new VillageProgressState(0);
            village.SetLevels(snapshot.villageLevels);

            return new PlayerProfile(
                snapshot.playerId,
                currency,
                energy,
                village,
                snapshot.revision,
                snapshot.processedImpactIds);
        }

        private int SpendCoinsUpTo(int requestedAmount)
        {
            int available = currency.GetCoins();
            int spendAmount = requestedAmount;
            if (spendAmount > available)
            {
                spendAmount = available;
            }

            if (spendAmount <= 0)
            {
                return 0;
            }

            if (!currency.TrySpend(spendAmount))
            {
                return 0;
            }

            return spendAmount;
        }

        private int SpendEnergyUpTo(int requestedAmount)
        {
            int available = energy.GetCurrent();
            int spendAmount = requestedAmount;
            if (spendAmount > available)
            {
                spendAmount = available;
            }

            if (spendAmount <= 0)
            {
                return 0;
            }

            if (!energy.TrySpend(spendAmount))
            {
                return 0;
            }

            return spendAmount;
        }

        private static string[] CreateProcessedImpactIdsSnapshot(HashSet<string> source)
        {
            if (source == null || source.Count == 0)
            {
                return Array.Empty<string>();
            }

            string[] snapshot = new string[source.Count];
            source.CopyTo(snapshot);
            Array.Sort(snapshot, StringComparer.Ordinal);
            return snapshot;
        }

        private void SeedProcessedImpactIds(IEnumerable<string> source)
        {
            if (source == null)
            {
                return;
            }

            foreach (string impactId in source)
            {
                if (string.IsNullOrWhiteSpace(impactId))
                {
                    continue;
                }

                processedImpactIds.Add(impactId.Trim());
            }
        }

        private static string NormalizePlayerId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "local_player";
            }

            return value.Trim();
        }
    }
}
