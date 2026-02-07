using System.Collections;
using Game.Cards;
using Game.Cards.Config;
using Game.Common.Time;
using Game.Services.Cards;
using Game.Services.Economy;
using Game.Services.Energy;
using Game.Services.Minigames;
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
        [SerializeField] private int regenIntervalSeconds = 300;
        [SerializeField] private int drawCost = 1;
        [SerializeField] private int startingCoins = 0;
        [SerializeField] private CardDeckSO deckConfig;
        [SerializeField] private string fallbackDeckResourcePath = "Cards/DefaultCardDeck";
        [SerializeField] private bool drawOnStartWithoutUi = true;
        [SerializeField] private bool autoRefreshUi = true;
        [SerializeField] private float uiRefreshIntervalSeconds = 1f;

        [Header("UI")]
        [SerializeField] private Slider energySlider;
        [SerializeField] private TMP_Text energyText;
        [SerializeField] private TMP_Text energyTimerText;
        [SerializeField] private TMP_Text coinsText;
        [SerializeField] private TMP_Text resultText;

        private EnergyService energyService;
        private CurrencyService currencyService;
        private DrawCardUseCase drawCardUseCase;
        private Coroutine uiRefreshRoutine;

        private void Awake()
        {
            TimeProvider timeProvider = new TimeProvider();

            energyService = new EnergyService(
                timeProvider,
                startEnergy,
                maxEnergy,
                regenIntervalSeconds,
                0);

            currencyService = new CurrencyService();
            currencyService.Add(startingCoins);

            DrawModifiersService modifiersService = new DrawModifiersService();
            IMinigameLauncher minigameLauncher = new NullMinigameLauncher();

            RewardContext rewardContext = new RewardContext(
                energyService,
                currencyService,
                modifiersService,
                minigameLauncher);

            RewardEffectFactory rewardEffectFactory = new RewardEffectFactory();
            CardDeckFactory cardDeckFactory = new CardDeckFactory(rewardEffectFactory);

            CardDeckSO resolvedDeckConfig = deckConfig;
            if (resolvedDeckConfig == null && string.IsNullOrWhiteSpace(fallbackDeckResourcePath) == false)
            {
                resolvedDeckConfig = Resources.Load<CardDeckSO>(fallbackDeckResourcePath);
            }

            if (resolvedDeckConfig == null)
            {
                Debug.LogWarning("No CardDeckSO was found. Using fallback in-memory deck.");
            }

            ICardDeck deck = cardDeckFactory.Create(resolvedDeckConfig);

            drawCardUseCase = new DrawCardUseCase(
                energyService,
                deck,
                rewardContext,
                drawCost);

            RefreshUi();
        }

        private void Start()
        {
            if (drawOnStartWithoutUi == false)
            {
                return;
            }

            if (HasAnyUiBinding())
            {
                return;
            }

            OnDrawButtonClicked();
        }

        private void OnEnable()
        {
            TryStartUiRefreshLoop();
        }

        private void OnDisable()
        {
            StopUiRefreshLoop();
        }

        public void OnDrawButtonClicked()
        {
            if (drawCardUseCase == null)
            {
                SetResult("Draw failed. Presenter is not initialized.");
                return;
            }

            CardDefinition drawnCard;
            bool success = drawCardUseCase.TryDraw(out drawnCard);

            if (success == false)
            {
                SetResult("Draw failed. Check energy.");
                RefreshUi();
                return;
            }

            RefreshUi();
            SetResult("Card: " + drawnCard.Id + " | Coins: " + currencyService.GetCoins());
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

            if (energySlider != null)
            {
                energySlider.wholeNumbers = true;
                energySlider.minValue = 0f;
                energySlider.maxValue = Mathf.Max(1, maxEnergyValue);
                energySlider.value = Mathf.Clamp(currentEnergy, 0, maxEnergyValue);
            }

            if (energyText != null)
            {
                energyText.text = "Energy: " + currentEnergy + "/" + maxEnergyValue;
            }

            if (energyTimerText != null)
            {
                if (currentEnergy >= maxEnergyValue)
                {
                    energyTimerText.text = "Energy Full";
                }
                else
                {
                    energyTimerText.text = "Next in: " + energyService.GetSecondsUntilNext() + "s";
                }
            }

            if (coinsText != null)
            {
                coinsText.text = "Coins: " + currencyService.GetCoins();
            }
        }

        private void SetResult(string message)
        {
            if (resultText != null)
            {
                resultText.text = message;
            }

            Debug.Log(message);
        }

        private bool HasAnyUiBinding()
        {
            return energySlider != null || energyText != null || energyTimerText != null || coinsText != null || resultText != null;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus == true)
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

        private void TryStartUiRefreshLoop()
        {
            if (autoRefreshUi == false)
            {
                return;
            }

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
    }
}
