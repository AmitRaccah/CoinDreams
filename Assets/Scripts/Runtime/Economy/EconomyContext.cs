using Game.Config.Player;
using Game.Domain.Economy;
using UnityEngine;

namespace Game.Runtime.Economy
{
    [DisallowMultipleComponent]
    public sealed class EconomyContext : MonoBehaviour
    {
        [Header("Initial Currency")]
        [SerializeField] private PlayerDataConfig playerDataConfig;
        [SerializeField] private int fallbackStartingCoins;
        [SerializeField] private bool persistAcrossScenes;

        private CurrencyService currencyService;
        private bool initialized;

        public CurrencyService CurrencyService
        {
            get
            {
                EnsureInitialized();
                return currencyService;
            }
        }

        public ICurrencyWallet Wallet
        {
            get { return CurrencyService; }
        }

        private void Awake()
        {
            EnsureInitialized();

            if (persistAcrossScenes)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            currencyService = new CurrencyService();

            int startingCoins = fallbackStartingCoins;
            if (playerDataConfig != null)
            {
                startingCoins = playerDataConfig.startingCurrency;
            }

            currencyService.Add(startingCoins);
        }
    }
}
