using System;
using Game.Domain.Economy;
using Game.Domain.Energy;
using Game.Domain.Player;
using Game.Domain.Time;
using Game.Domain.Village;
using UnityEngine;

namespace Game.Runtime.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerRuntimeContext : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private string playerId = "local_player";

        [Header("Initial Economy")]
        [SerializeField] private int startingCoins;

        [Header("Initial Energy")]
        [SerializeField] private int startingEnergy = 5;
        [SerializeField] private int maxEnergy = 10;
        [SerializeField] private int maxStoredEnergy = 20;
        [SerializeField] private int regenIntervalSeconds = 300;
        [SerializeField] private long lastRegenUtcTicks;

        [Header("Initial Village")]
        [SerializeField] private int initialVillageBuildingCount = 1;

        [Header("Runtime")]
        [SerializeField] private bool persistAcrossScenes = true;

        private PlayerProfile profile;
        private bool initialized;

        public PlayerProfile Profile
        {
            get
            {
                EnsureInitialized();
                return profile;
            }
        }

        public CurrencyService CurrencyService
        {
            get { return Profile.Currency; }
        }

        public EnergyService EnergyService
        {
            get { return Profile.Energy; }
        }

        public ICurrencyWallet Wallet
        {
            get { return CurrencyService; }
        }

        public VillageProgressState VillageProgressState
        {
            get { return Profile.Village; }
        }

        private void Awake()
        {
            EnsureInitialized();

            if (persistAcrossScenes)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        public void EnsureVillageCapacity(int buildingCount)
        {
            EnsureInitialized();
            profile.EnsureVillageCapacity(buildingCount);
        }

        public PlayerImpactApplyResult ApplyExternalImpact(PlayerImpact impact)
        {
            EnsureInitialized();
            return profile.ApplyExternalImpact(impact);
        }

        public PlayerImpactApplyResult[] ApplyExternalImpacts(PlayerImpact[] impacts)
        {
            EnsureInitialized();

            if (impacts == null || impacts.Length == 0)
            {
                return Array.Empty<PlayerImpactApplyResult>();
            }

            PlayerImpactApplyResult[] results = new PlayerImpactApplyResult[impacts.Length];

            int i;
            for (i = 0; i < impacts.Length; i++)
            {
                results[i] = profile.ApplyExternalImpact(impacts[i]);
            }

            return results;
        }

        public PlayerProfileSnapshot CreateSnapshot()
        {
            EnsureInitialized();
            return profile.CreateSnapshot();
        }

        public void LoadSnapshot(PlayerProfileSnapshot snapshot)
        {
            if (snapshot == null)
            {
                Debug.LogWarning("[PlayerRuntimeContext] LoadSnapshot called with null snapshot.", this);
                return;
            }

            TimeProvider timeProvider = new TimeProvider();
            profile = PlayerProfile.FromSnapshot(snapshot, timeProvider);
            initialized = true;
        }

        private void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;

            TimeProvider timeProvider = new TimeProvider();

            EnergyService energy = new EnergyService(
                timeProvider,
                startingEnergy,
                maxEnergy,
                maxStoredEnergy,
                regenIntervalSeconds,
                lastRegenUtcTicks);

            CurrencyService currency = new CurrencyService();
            if (startingCoins > 0)
            {
                currency.Add(startingCoins);
            }

            VillageProgressState village = new VillageProgressState(initialVillageBuildingCount);

            profile = new PlayerProfile(
                playerId,
                currency,
                energy,
                village,
                0,
                null);
        }
    }
}
