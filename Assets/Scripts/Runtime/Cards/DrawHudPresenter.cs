#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Domain.Cards;
using Game.Domain.Economy;
using Game.Domain.Energy;
using Game.Runtime.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Game.Runtime.Cards
{
    [DisallowMultipleComponent]
    public sealed class DrawHudPresenter : MonoBehaviour, IDrawResultSink
    {
        [Header("Config")]
        [SerializeField] private float uiRefreshIntervalSeconds = 1f;

        [Inject] private PlayerRuntimeContext? playerRuntimeContext;
        [Inject] private CardDrawHudReferences? hudReferences;

        private Slider? energySlider;
        private TMP_Text? energyText;
        private TMP_Text? energyTimerText;
        private TMP_Text? extraEnergyText;
        private TMP_Text? coinsText;
        private TMP_Text? resultText;

        private EnergyService? energyService;
        private CurrencyService? currencyService;
        private CancellationTokenSource? uiRefreshCts;
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
            BindHudReferences();

            if (playerRuntimeContext == null)
            {
                return;
            }

            RebuildRuntimeBindings();
            RefreshAllUi();
        }

        private void BindHudReferences()
        {
            if (hudReferences == null)
            {
                return;
            }

            energySlider = hudReferences.EnergySlider;
            energyText = hudReferences.EnergyText;
            energyTimerText = hudReferences.EnergyTimerText;
            extraEnergyText = hudReferences.ExtraEnergyText;
            coinsText = hudReferences.CoinsText;
            resultText = hudReferences.ResultText;
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
            if (energyService != null)
            {
                RefreshTimerUi(energyService.GetSecondsUntilNext());
            }
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
            if (uiRefreshCts != null)
            {
                return;
            }

            this.uiRefreshCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            RefreshUiLoopAsync(this.uiRefreshCts.Token).Forget(ex =>
            {
                if (ex is OperationCanceledException) return;
                Debug.LogException(ex, this);
            });
        }

        private void StopUiRefreshLoop()
        {
            if (uiRefreshCts == null)
            {
                return;
            }

            uiRefreshCts.Cancel();
            uiRefreshCts.Dispose();
            uiRefreshCts = null;
        }

        private async UniTask RefreshUiLoopAsync(CancellationToken token)
        {
            float interval = uiRefreshIntervalSeconds;
            if (interval <= 0f)
            {
                interval = 1f;
            }

            TimeSpan delay = TimeSpan.FromSeconds(interval);

            while (!token.IsCancellationRequested)
            {
                if (energyService != null)
                {
                    energyService.ApplyRegen();
                    RefreshTimerUi(energyService.GetSecondsUntilNext());
                }

                await UniTask.Delay(delay, DelayType.Realtime, PlayerLoopTiming.Update, token);
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
