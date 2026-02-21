using System.Collections;
using Game.Domain.Cards;
using Game.Config.Cards;
using Game.Domain.Time;
using Game.Domain.Economy;
using Game.Domain.Energy;
using Game.Domain.Minigames;
using Game.Runtime.Economy;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.Cards
{
    public sealed class DrawGamePresenter : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private int startEnergy = 5;
        [SerializeField] private int maxEnergy = 10;
        [SerializeField] private int maxStoredEnergy = 20;
        [SerializeField] private int regenIntervalSeconds = 300;
        [SerializeField] private int drawCost = 1;
        [SerializeField] private int startingCoins = 0;
        [SerializeField] private CardDeckSO deckConfig;
        [SerializeField] private float uiRefreshIntervalSeconds = 1f;
        [SerializeField] private EconomyContext economyContext;

        [Header("UI")]
        [SerializeField] private Slider energySlider;
        [SerializeField] private TMP_Text energyText;
        [SerializeField] private TMP_Text energyTimerText;
        [SerializeField] private TMP_Text extraEnergyText;
        [SerializeField] private TMP_Text coinsText;
        [SerializeField] private TMP_Text resultText;

        private EnergyService energyService;
        private CurrencyService currencyService;
        private DrawCardUseCase drawCardUseCase;
        private Coroutine uiRefreshRoutine;
        private bool uiCacheInitialized;
        private int cachedBaseEnergy;
        private int cachedMaxEnergy;
        private int cachedExtraEnergy;
        private int cachedCoins;
        private int cachedSecondsUntilNext;

        private void Awake()
        {
            TimeProvider timeProvider = new TimeProvider();

            energyService = new EnergyService(
                timeProvider,
                startEnergy,
                maxEnergy,
                maxStoredEnergy,
                regenIntervalSeconds,
                0);

            currencyService = ResolveCurrencyService();

            DrawModifiersService modifiersService = new DrawModifiersService();
            IMinigameLauncher minigameLauncher = NullMinigameLauncher.Instance;

            RewardContext rewardContext = new RewardContext(
                energyService,
                currencyService,
                modifiersService,
                minigameLauncher);

            RewardEffectFactory rewardEffectFactory = new RewardEffectFactory();
            CardDeckFactory cardDeckFactory = new CardDeckFactory(rewardEffectFactory);
            ICardDeck deck = cardDeckFactory.Create(deckConfig);

            drawCardUseCase = new DrawCardUseCase(
                energyService,
                deck,
                rewardContext,
                drawCost);

            RefreshUi();
        }

        private void OnEnable()
        {
            StartUiRefreshLoop();
        }

        private void OnDisable()
        {
            StopUiRefreshLoop();
        }

        public void OnDrawButtonClicked()
        {
            if (drawCardUseCase == null)
            {
                SetResult("Draw failed.");
                return;
            }

            CardDefinition drawnCard;
            bool success = drawCardUseCase.TryDraw(out drawnCard);

            if (success == false)
            {
                SetResult("Not enough energy.");
                RefreshUi();
                Debug.LogWarning("[DRAW] Failed: not enough energy.");
                return;
            }

            RefreshUi();
            SetResult("Card: " + drawnCard.Id);
            Debug.Log("[DRAW] Card=" + drawnCard.Id + " | Coins=" + currencyService.GetCoins());
        }

        private void RefreshUi()
        {
            if (energyService == null || currencyService == null)
            {
                return;
            }

            energyService.ApplyRegen();

            int currentEnergy = energyService.GetCurrent();
            int maxEnergyValue = energyService.GetMax();
            int extraEnergy = energyService.GetExtra();
            int baseEnergy = currentEnergy - extraEnergy;
            int secondsUntilNext = energyService.GetSecondsUntilNext();
            int coins = currencyService.GetCoins();

            bool energyChanged = !uiCacheInitialized
                || cachedBaseEnergy != baseEnergy
                || cachedMaxEnergy != maxEnergyValue;
            bool extraChanged = !uiCacheInitialized || cachedExtraEnergy != extraEnergy;
            bool timerChanged = !uiCacheInitialized || cachedSecondsUntilNext != secondsUntilNext;
            bool coinsChanged = !uiCacheInitialized || cachedCoins != coins;

            if (energySlider != null)
            {
                if (!uiCacheInitialized || cachedMaxEnergy != maxEnergyValue)
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

            if (energyTimerText != null && timerChanged)
            {
                energyTimerText.SetText("Next in: {0:0}s", secondsUntilNext);
            }

            if (coinsText != null && coinsChanged)
            {
                coinsText.SetText("Coins: {0:0}", coins);
            }

            cachedBaseEnergy = baseEnergy;
            cachedMaxEnergy = maxEnergyValue;
            cachedExtraEnergy = extraEnergy;
            cachedCoins = coins;
            cachedSecondsUntilNext = secondsUntilNext;
            uiCacheInitialized = true;
        }

        private void SetResult(string message)
        {
            if (resultText != null)
            {
                resultText.text = message;
            }
        }

        private CurrencyService ResolveCurrencyService()
        {
            EconomyContext context = economyContext;
            if (context != null)
            {
                return context.CurrencyService;
            }

            CurrencyService localService = new CurrencyService();
            localService.Add(startingCoins);
            return localService;
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
                RefreshUi();
                yield return delay;
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
            {
                RefreshUi();
            }
        }

        private void OnApplicationPause(bool pause)
        {
            if (pause == false)
            {
                RefreshUi();
            }
        }
    }
}
