#nullable enable
using System;
using System.Collections.Generic;
using Game.Domain.Economy;
using Game.Domain.Energy;
using Game.Domain.Player;
using Game.Domain.Time;
using Game.Domain.Village;
using Game.Infrastructure.Persistence;
using UnityEngine;
using VContainer;

namespace Game.Runtime.Player
{
    public interface IPendingImpactProvider
    {
        IReadOnlyList<PlayerImpact> DrainPending();
    }

    [DisallowMultipleComponent]
    public sealed class PlayerRuntimeContext : MonoBehaviour, IPlayerStateGateway
    {
        [Header("Identity")]
        [SerializeField] private string playerId = PlayerDefaults.PlaceholderPlayerId;

        [Header("Initial Economy")]
        [SerializeField] private int startingCoins;

        [Header("Initial Energy")]
        [SerializeField] private int startingEnergy = EnergyDefaults.DefaultStartingEnergy;
        [SerializeField] private int maxEnergy = EnergyDefaults.DefaultMaxEnergy;
        [SerializeField] private int regenIntervalSeconds = EnergyDefaults.DefaultRegenIntervalSeconds;
        [SerializeField] private long lastRegenUtcTicks;

        [Header("Initial Village")]
        [SerializeField] private int initialVillageBuildingCount = 1;

        [Header("Runtime")]
        [SerializeField] private bool persistAcrossScenes = true;

        [Header("Impact Inbox")]
        [SerializeField] private MonoBehaviour? pendingImpactProviderSource;

        private PlayerProfile? profile;
        private bool initialized;
        private IPendingImpactProvider? pendingImpactProvider;

        [Inject] private ITimeProvider? injectedTimeProvider;

        // Fires AFTER the new profile is bound. Subscribers may unsubscribe-then-resubscribe to leaf services
        // (EnergyChanged, CoinsChanged) here. Subscribers must defensively check `this != null` since
        // they may receive this during scene teardown.
        public event Action? ProfileReplaced;
        public event Action? StateChanged;

        public PlayerProfile Profile
        {
            get
            {
                EnsureInitialized();
                return profile!;
            }
        }

        // Concrete service getters below are intended for Domain-side engine consumers
        // (e.g. VillageUpgradeRuntime constructing VillageUpgradeService). UI/view layers
        // MUST consume the IReadOnly* views (CurrencyView/EnergyView/VillageView) instead.
        public ICurrencyWallet CurrencyService
        {
            get { return Profile.Currency; }
        }

        public IEnergyService EnergyService
        {
            get { return Profile.Energy; }
        }

        public ICurrencyWallet Wallet
        {
            get { return CurrencyService; }
        }

        public IVillageProgressStateWriter VillageProgressState
        {
            get { return Profile.Village; }
        }

        public IReadOnlyCurrencyWallet CurrencyView
        {
            get { return Profile.Currency; }
        }

        public IReadOnlyEnergyService EnergyView
        {
            get { return Profile.Energy; }
        }

        public IReadOnlyVillageProgressState VillageView
        {
            get { return Profile.Village; }
        }

        public void RefreshRegen()
        {
            EnsureInitialized();
            profile!.ApplyTimeBasedRegen();
        }

        public string PlayerId
        {
            get { return Profile.PlayerId; }
        }

        public int CurrentRevision
        {
            get { return Profile.Revision; }
        }

        private void Awake()
        {
            EnsureInitialized();

            if (persistAcrossScenes)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private void Start()
        {
            PollImpactInbox();
        }

        public void EnsureVillageCapacity(int buildingCount)
        {
            EnsureInitialized();
            profile!.EnsureVillageCapacity(buildingCount);
        }

        public PlayerImpactApplyResult ApplyExternalImpact(PlayerImpact impact)
        {
            EnsureInitialized();
            return profile!.ApplyExternalImpact(impact);
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
                results[i] = profile!.ApplyExternalImpact(impacts[i]);
            }

            return results;
        }

        public void PollImpactInbox()
        {
            if (this == null || gameObject == null)
            {
                return;
            }

            if (!TryResolveImpactProvider())
            {
                return;
            }

            IReadOnlyList<PlayerImpact> pending = pendingImpactProvider!.DrainPending();
            if (pending == null || pending.Count == 0)
            {
                return;
            }

            EnsureInitialized();

            int i;
            for (i = 0; i < pending.Count; i++)
            {
                PlayerImpact impact = pending[i];
                if (impact == null)
                {
                    continue;
                }

                profile!.ApplyExternalImpact(impact);
            }
        }

        public PlayerProfileSnapshot CreateSnapshot()
        {
            EnsureInitialized();
            return profile!.CreateSnapshot();
        }

        public void LoadSnapshot(PlayerProfileSnapshot snapshot)
        {
            if (snapshot == null)
            {
                Debug.LogWarning("[PlayerRuntimeContext] LoadSnapshot called with null snapshot.", this);
                return;
            }

            ITimeProvider timeProvider = ResolveTimeProvider();
            PlayerProfile loadedProfile = PlayerProfile.FromSnapshot(snapshot, timeProvider);
            ReplaceProfile(loadedProfile, true);
            PollImpactInbox();
        }

        private bool TryResolveImpactProvider()
        {
            if (pendingImpactProvider != null)
            {
                return true;
            }

            if (pendingImpactProviderSource == null)
            {
                return false;
            }

            pendingImpactProvider = pendingImpactProviderSource as IPendingImpactProvider;
            return pendingImpactProvider != null;
        }

        private void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;

            ITimeProvider timeProvider = ResolveTimeProvider();

            EnergyService energy = new EnergyService(
                timeProvider,
                startingEnergy,
                maxEnergy,
                regenIntervalSeconds,
                lastRegenUtcTicks);

            CurrencyService currency = new CurrencyService();
            if (startingCoins > 0)
            {
                currency.Add(startingCoins);
            }

            VillageProgressState village = new VillageProgressState(initialVillageBuildingCount);

            PlayerProfile initialProfile = new PlayerProfile(
                playerId,
                currency,
                energy,
                village,
                0,
                null);

            ReplaceProfile(initialProfile, false);
        }

        // Fallback `new TimeProvider()` mirrors FirebasePlayerPersistenceRuntime: defensive pattern for
        // ad-hoc editor scenes instantiated outside any LifetimeScope where DI hasn't run.
        private ITimeProvider ResolveTimeProvider()
        {
            return injectedTimeProvider ?? new TimeProvider();
        }

        private void ReplaceProfile(PlayerProfile newProfile, bool notifyReplacement)
        {
            // Unity-nullity guard: scene teardown can leave this in a destroyed-but-not-collected state.
            if (this == null || gameObject == null)
            {
                return;
            }

            if (newProfile == null)
            {
                return;
            }

            if (profile != null)
            {
                profile.Changed -= HandleProfileChanged;
            }

            profile = newProfile;
            profile.Changed += HandleProfileChanged;
            initialized = true;

            if (notifyReplacement)
            {
                Action? replacementHandler = ProfileReplaced;
                if (replacementHandler != null)
                {
                    replacementHandler();
                }
            }

            NotifyStateChanged();
        }

        private void HandleProfileChanged()
        {
            NotifyStateChanged();
        }

        private void NotifyStateChanged()
        {
            Action? handler = StateChanged;
            if (handler != null)
            {
                handler();
            }
        }
    }
}
