using System.Collections;
using Game.Domain.Cards;
using Game.Domain.Economy;
using Game.Domain.Energy;
using Game.Runtime.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.Cards
{
    [DisallowMultipleComponent]
    public sealed class DrawHudPresenter : MonoBehaviour, IDrawResultSink
    {
        [Header("Config")]
        [SerializeField] private float uiRefreshIntervalSeconds = 1f;
        [SerializeField] private PlayerRuntimeContext playerRuntimeContext;

        [Header("UI")]
        [SerializeField] private Slider energySlider;
        [SerializeField] private TMP_Text energyText;
        [SerializeField] private TMP_Text energyTimerText;
        [SerializeField] private TMP_Text extraEnergyText;
        [SerializeField] private TMP_Text coinsText;
        [SerializeField] private TMP_Text resultText;

        private EnergyService energyService;
        private CurrencyService currencyService;
        private Coroutine uiRefreshRoutine;
        private bool isSubscribed;
        private bool energyUiCacheInitialized;
        private bool coinsUiCacheInitialized;
        private bool timerUiCacheInitialized;
        private int cachedBaseEnergy;
        private int cachedMaxEnergy;
        private int cachedExtraEnergy;
        private int cachedCoins;
        private int cachedSecondsUntilNext;
        private bool isContextSubscribed;

        private void Awake()
        {
            if (!TryResolvePlayerContext())
            {
                return;
            }

            RebuildRuntimeBindings();
            RefreshAllUi();
        }

        private void OnEnable()
        {
            SubscribeToRuntimeContextEvents();
            SubscribeToStateEvents();
            StartUiRefreshLoop();
            RefreshAllUi();
        }

        private void OnDisable()
        {
            StopUiRefreshLoop();
            UnsubscribeFromStateEvents();
            UnsubscribeFromRuntimeContextEvents();
        }

        public void Configure(
            PlayerRuntimeContext playerRuntimeContext,
            float uiRefreshIntervalSeconds,
            Slider energySlider,
            TMP_Text energyText,
            TMP_Text energyTimerText,
            TMP_Text extraEnergyText,
            TMP_Text coinsText,
            TMP_Text resultText)
        {
            this.playerRuntimeContext = playerRuntimeContext;
            this.uiRefreshIntervalSeconds = uiRefreshIntervalSeconds;
            this.energySlider = energySlider;
            this.energyText = energyText;
            this.energyTimerText = energyTimerText;
            this.extraEnergyText = extraEnergyText;
            this.coinsText = coinsText;
            this.resultText = resultText;

            if (!TryResolvePlayerContext())
            {
                return;
            }

            RebuildRuntimeBindings();
            RefreshAllUi();

            if (isActiveAndEnabled)
            {
                StopUiRefreshLoop();
                StartUiRefreshLoop();
            }
        }

        public void Present(AuthoritativeDrawResult result)
        {
            SetResult(AuthoritativeDrawResultFormatter.Format(result));
        }

        private void RefreshAllUi()
        {
            if (energyService == null || currencyService == null)
            {
                return;
            }

            int currentEnergy = energyService.GetCurrent();
            int maxEnergyValue = energyService.GetMax();
            int extraEnergy = energyService.GetExtra();
            int coins = currencyService.GetCoins();
            int secondsUntilNext = energyService.GetSecondsUntilNext();

            RefreshEnergyUi(currentEnergy, maxEnergyValue, extraEnergy);
            RefreshCoinsUi(coins);
            RefreshTimerUi(secondsUntilNext);
        }

        private void RefreshEnergyUi(int currentEnergy, int maxEnergyValue, int extraEnergy)
        {
            int baseEnergy = currentEnergy - extraEnergy;

            bool energyChanged = !energyUiCacheInitialized
                || cachedBaseEnergy != baseEnergy
                || cachedMaxEnergy != maxEnergyValue;
            bool extraChanged = !energyUiCacheInitialized || cachedExtraEnergy != extraEnergy;

            if (energySlider != null)
            {
                if (!energyUiCacheInitialized || cachedMaxEnergy != maxEnergyValue)
                {
                    energySlider.wholeNumbers = true;
                    energySlider.minValue = 0f;
                    energySlider.maxValue = Mathf.Max(1, maxEnergyValue);
                }

                if (energyChanged)
                {
                    energySlider.value = Mathf.Clamp(baseEnergy, 0, maxEnergyValue);
                }
            }

            if (energyText != null && energyChanged)
            {
                energyText.SetText("Energy: {0:0}/{1:0}", baseEnergy, maxEnergyValue);
            }

            if (extraEnergyText != null && extraChanged)
            {
                extraEnergyText.SetText("Extra: +{0:0}", extraEnergy);
            }

            cachedBaseEnergy = baseEnergy;
            cachedMaxEnergy = maxEnergyValue;
            cachedExtraEnergy = extraEnergy;
            energyUiCacheInitialized = true;
        }

        private void RefreshCoinsUi(int coins)
        {
            bool coinsChanged = !coinsUiCacheInitialized || cachedCoins != coins;
            if (coinsText != null && coinsChanged)
            {
                coinsText.SetText("Coins: {0:0}", coins);
            }

            cachedCoins = coins;
            coinsUiCacheInitialized = true;
        }

        private void RefreshTimerUi(int secondsUntilNext)
        {
            bool timerChanged = !timerUiCacheInitialized || cachedSecondsUntilNext != secondsUntilNext;
            if (energyTimerText != null && timerChanged)
            {
                energyTimerText.SetText("Next in: {0:0}s", secondsUntilNext);
            }

            cachedSecondsUntilNext = secondsUntilNext;
            timerUiCacheInitialized = true;
        }

        private void HandleEnergyChanged(int currentEnergy, int maxEnergyValue, int extraEnergy)
        {
            RefreshEnergyUi(currentEnergy, maxEnergyValue, extraEnergy);
            RefreshTimerUi(energyService.GetSecondsUntilNext());
        }

        private void HandleCoinsChanged(int coins)
        {
            RefreshCoinsUi(coins);
        }

        private void HandleProfileReplaced()
        {
            UnsubscribeFromStateEvents();
            RebuildRuntimeBindings();
            SubscribeToStateEvents();
            RefreshAllUi();
        }

        private void SubscribeToStateEvents()
        {
            if (isSubscribed || energyService == null || currencyService == null)
            {
                return;
            }

            energyService.EnergyChanged += HandleEnergyChanged;
            currencyService.CoinsChanged += HandleCoinsChanged;
            isSubscribed = true;
        }

        private void UnsubscribeFromStateEvents()
        {
            if (!isSubscribed || energyService == null || currencyService == null)
            {
                return;
            }

            energyService.EnergyChanged -= HandleEnergyChanged;
            currencyService.CoinsChanged -= HandleCoinsChanged;
            isSubscribed = false;
        }

        private void SetResult(string message)
        {
            if (resultText != null)
            {
                resultText.text = message;
            }
        }

        private bool TryResolvePlayerContext()
        {
            if (playerRuntimeContext != null)
            {
                return true;
            }

            playerRuntimeContext = FindFirstObjectByType<PlayerRuntimeContext>();
            if (playerRuntimeContext != null)
            {
                return true;
            }

            GameObject runtimeContextObject = new GameObject("PlayerRuntimeContext");
            playerRuntimeContext = runtimeContextObject.AddComponent<PlayerRuntimeContext>();
            return playerRuntimeContext != null;
        }

        private void RebuildRuntimeBindings()
        {
            if (playerRuntimeContext == null)
            {
                energyService = null;
                currencyService = null;
                return;
            }

            energyService = playerRuntimeContext.EnergyService;
            currencyService = playerRuntimeContext.CurrencyService;

            if (energyService != null)
            {
                energyService.ApplyRegen();
            }
        }

        private void SubscribeToRuntimeContextEvents()
        {
            if (isContextSubscribed || playerRuntimeContext == null)
            {
                return;
            }

            playerRuntimeContext.ProfileReplaced += HandleProfileReplaced;
            isContextSubscribed = true;
        }

        private void UnsubscribeFromRuntimeContextEvents()
        {
            if (!isContextSubscribed || playerRuntimeContext == null)
            {
                return;
            }

            playerRuntimeContext.ProfileReplaced -= HandleProfileReplaced;
            isContextSubscribed = false;
        }

        private void StartUiRefreshLoop()
        {
            if (uiRefreshRoutine != null)
            {
                return;
            }

            uiRefreshRoutine = StartCoroutine(RefreshUiLoop());
        }

        private void StopUiRefreshLoop()
        {
            if (uiRefreshRoutine == null)
            {
                return;
            }

            StopCoroutine(uiRefreshRoutine);
            uiRefreshRoutine = null;
        }

        private IEnumerator RefreshUiLoop()
        {
            float interval = uiRefreshIntervalSeconds;
            if (interval <= 0f)
            {
                interval = 1f;
            }

            WaitForSecondsRealtime delay = new WaitForSecondsRealtime(interval);

            while (true)
            {
                if (energyService != null)
                {
                    energyService.ApplyRegen();
                    RefreshTimerUi(energyService.GetSecondsUntilNext());
                }

                yield return delay;
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus && energyService != null)
            {
                energyService.ApplyRegen();
                RefreshAllUi();
            }
        }

        private void OnApplicationPause(bool pause)
        {
            if (!pause && energyService != null)
            {
                energyService.ApplyRegen();
                RefreshAllUi();
            }
        }
    }
}
