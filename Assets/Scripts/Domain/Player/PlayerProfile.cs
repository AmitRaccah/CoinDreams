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
        private const int MaxProcessedImpactIds = 10000;

        private readonly string playerId;
        private readonly CurrencyService currency;
        private readonly EnergyService energy;
        private readonly VillageProgressState village;
        private readonly HashSet<string> processedImpactSet;
        private readonly Queue<string> processedImpactOrder;
        private int revision;
        private int batchedMutationDepth;
        private bool hasBatchedMutation;
        private bool isInitializing;
        public event Action Changed;

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

            isInitializing = true;
            this.playerId = NormalizePlayerId(playerId);
            this.currency = currency;
            this.energy = energy;
            this.village = village;
            this.revision = revision < 0 ? 0 : revision;
            processedImpactSet = new HashSet<string>(StringComparer.Ordinal);
            processedImpactOrder = new Queue<string>();
            SeedProcessedImpactIds(seedProcessedImpactIds);
            SubscribeToStateChanges();
            isInitializing = false;
        }

        public string PlayerId
        {
            get { return playerId; }
        }

        public ICurrencyWallet Currency
        {
            get { return currency; }
        }

        public IEnergyService Energy
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

            return processedImpactSet.Contains(impactId.Trim());
        }

        public void ApplyTimeBasedRegen()
        {
            energy.ApplyRegen();
        }

        public PlayerImpactApplyResult ApplyExternalImpact(PlayerImpact impact)
        {
            if (impact == null)
            {
                return PlayerImpactApplyResult.Invalid(
                    string.Empty,
                    PlayerImpactType.None,
                    0,
                    "Impact is null.");
            }

            PlayerImpact safeImpact = impact.Clone();

            if (string.IsNullOrWhiteSpace(safeImpact.impactId))
            {
                return PlayerImpactApplyResult.Invalid(
                    string.Empty,
                    safeImpact.impactType,
                    safeImpact.amount,
                    "ImpactId is required.");
            }

            string impactId = safeImpact.impactId.Trim();
            if (processedImpactSet.Contains(impactId))
            {
                return PlayerImpactApplyResult.Duplicate(impactId, safeImpact.impactType);
            }

            if (safeImpact.amount <= 0)
            {
                return PlayerImpactApplyResult.Invalid(
                    impactId,
                    safeImpact.impactType,
                    safeImpact.amount,
                    "Impact amount must be greater than zero.");
            }

            if (safeImpact.impactType == PlayerImpactType.None)
            {
                return PlayerImpactApplyResult.Invalid(
                    impactId,
                    safeImpact.impactType,
                    safeImpact.amount,
                    "Unsupported impact type.");
            }

            RecordProcessedImpactId(impactId);

            int requestedAmount = safeImpact.amount;
            int appliedAmount = 0;
            int coinsDelta = 0;
            int energyDelta = 0;

            BeginMutationBatch();
            try
            {
                energy.ApplyRegen();

                if (safeImpact.impactType == PlayerImpactType.CoinsGranted)
                {
                    int beforeCoins = currency.GetCoins();
                    currency.Add(requestedAmount);
                    int afterCoins = currency.GetCoins();
                    appliedAmount = afterCoins - beforeCoins;
                    coinsDelta = appliedAmount;
                }
                else if (safeImpact.impactType == PlayerImpactType.CoinsStolen)
                {
                    appliedAmount = SpendCoinsUpTo(requestedAmount);
                    coinsDelta = -appliedAmount;
                }
                else if (safeImpact.impactType == PlayerImpactType.EnergyGranted)
                {
                    int beforeEnergy = energy.GetCurrent();
                    energy.Add(requestedAmount);
                    int afterEnergy = energy.GetCurrent();
                    appliedAmount = afterEnergy - beforeEnergy;
                    energyDelta = appliedAmount;
                }
                else if (safeImpact.impactType == PlayerImpactType.EnergyRemoved)
                {
                    appliedAmount = SpendEnergyUpTo(requestedAmount);
                    energyDelta = -appliedAmount;
                }
                else
                {
                    UnrecordProcessedImpactId(impactId);
                    return PlayerImpactApplyResult.Invalid(
                        impactId,
                        safeImpact.impactType,
                        requestedAmount,
                        "Unsupported impact type.");
                }

                if (coinsDelta == 0 && energyDelta == 0)
                {
                    return PlayerImpactApplyResult.AppliedNothing(impactId, safeImpact.impactType);
                }

                MarkChanged();
            }
            catch
            {
                UnrecordProcessedImpactId(impactId);
                throw;
            }
            finally
            {
                EndMutationBatch();
            }

            bool isPartial = appliedAmount < requestedAmount;
            return PlayerImpactApplyResult.Applied(
                impactId,
                safeImpact.impactType,
                requestedAmount,
                appliedAmount,
                coinsDelta,
                energyDelta,
                isPartial);
        }

        /// <summary>
        /// Pure snapshot accessor. Does NOT mutate state and does NOT bump revision.
        /// Callers wanting up-to-date regen must invoke <see cref="ApplyTimeBasedRegen"/> first.
        /// </summary>
        public PlayerProfileSnapshot CreateSnapshot()
        {
            PlayerProfileSnapshot snapshot = new PlayerProfileSnapshot();
            snapshot.playerId = playerId;
            snapshot.revision = revision;
            snapshot.coins = currency.GetCoins();
            snapshot.currentEnergy = energy.GetCurrent();
            snapshot.regenMaxEnergy = energy.GetMax();
            snapshot.regenIntervalSeconds = energy.GetRegenIntervalSeconds();
            snapshot.lastRegenUtcTicks = energy.GetLastRegenTicks();
            snapshot.villageLevels = village.GetLevelsSnapshot();
            snapshot.processedImpactIds = CreateProcessedImpactIdsSnapshot();
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

            NormalizeSnapshot(snapshot);

            if (snapshot.coins < 0)
            {
                throw new InvalidOperationException("Snapshot has negative coins balance: " + snapshot.coins);
            }

            CurrencyService currency = new CurrencyService(snapshot.coins);

            EnergyService energy = new EnergyService(
                timeProvider,
                snapshot.currentEnergy,
                snapshot.regenMaxEnergy,
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

        private static void NormalizeSnapshot(PlayerProfileSnapshot snapshot)
        {
            if (snapshot.regenMaxEnergy <= 0)
            {
                snapshot.regenMaxEnergy = EnergyDefaults.DefaultMaxEnergy;
            }

            if (snapshot.regenIntervalSeconds <= 0)
            {
                snapshot.regenIntervalSeconds = EnergyDefaults.DefaultRegenIntervalSeconds;
            }
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

        private void RecordProcessedImpactId(string impactId)
        {
            if (!processedImpactSet.Add(impactId))
            {
                return;
            }

            processedImpactOrder.Enqueue(impactId);

            while (processedImpactOrder.Count > MaxProcessedImpactIds)
            {
                string oldest = processedImpactOrder.Dequeue();
                processedImpactSet.Remove(oldest);
            }
        }

        private void UnrecordProcessedImpactId(string impactId)
        {
            if (!processedImpactSet.Remove(impactId))
            {
                return;
            }

            int count = processedImpactOrder.Count;
            for (int i = 0; i < count; i++)
            {
                string current = processedImpactOrder.Dequeue();
                if (!string.Equals(current, impactId, StringComparison.Ordinal))
                {
                    processedImpactOrder.Enqueue(current);
                }
            }
        }

        private string[] CreateProcessedImpactIdsSnapshot()
        {
            if (processedImpactOrder.Count == 0)
            {
                return Array.Empty<string>();
            }

            return processedImpactOrder.ToArray();
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

                string normalized = impactId.Trim();
                if (processedImpactSet.Add(normalized))
                {
                    processedImpactOrder.Enqueue(normalized);
                }

                while (processedImpactOrder.Count > MaxProcessedImpactIds)
                {
                    string oldest = processedImpactOrder.Dequeue();
                    processedImpactSet.Remove(oldest);
                }
            }
        }

        private void SubscribeToStateChanges()
        {
            currency.CoinsChanged += HandleCoinsChanged;
            energy.EnergyChanged += HandleEnergyChanged;
            village.Changed += HandleVillageChanged;
        }

        private void HandleCoinsChanged(int _)
        {
            MarkChanged();
        }

        private void HandleEnergyChanged(int _, int __, int ___)
        {
            MarkChanged();
        }

        private void HandleVillageChanged()
        {
            MarkChanged();
        }

        private void BeginMutationBatch()
        {
            batchedMutationDepth++;
        }

        private void EndMutationBatch()
        {
            if (batchedMutationDepth <= 0)
            {
                batchedMutationDepth = 0;
                hasBatchedMutation = false;
                return;
            }

            batchedMutationDepth--;
            if (batchedMutationDepth == 0 && hasBatchedMutation)
            {
                hasBatchedMutation = false;
                IncrementRevisionAndNotify();
            }
        }

        private void MarkChanged()
        {
            if (isInitializing)
            {
                return;
            }

            if (batchedMutationDepth > 0)
            {
                hasBatchedMutation = true;
                return;
            }

            IncrementRevisionAndNotify();
        }

        private void IncrementRevisionAndNotify()
        {
            revision++;

            Action handler = Changed;
            if (handler != null)
            {
                handler();
            }
        }

        private static string NormalizePlayerId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return PlayerDefaults.PlaceholderPlayerId;
            }

            return value.Trim();
        }
    }
}
