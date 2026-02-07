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
        [SerializeField] private int maxStoredEnergy = 20;
        [SerializeField] private int regenIntervalSeconds = 300;
        [SerializeField] private int drawCost = 1;
        [SerializeField] private int startingCoins = 0;
        [SerializeField] private CardDeckSO deckConfig;
        [SerializeField] private float uiRefreshIntervalSeconds = 1f;

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

            if (energySlider != null)
            {
                energySlider.wholeNumbers = true;
                energySlider.minValue = 0f;
                energySlider.maxValue = Mathf.Max(1, maxEnergyValue);
                energySlider.value = Mathf.Clamp(baseEnergy, 0, maxEnergyValue);
            }

            if (energyText != null)
            {
                energyText.text = "Energy: " + baseEnergy + "/" + maxEnergyValue;
            }

            if (extraEnergyText != null)
            {
                extraEnergyText.text = "Extra: +" + extraEnergy;
            }

            if (energyTimerText != null)
            {
                int secondsUntilNext = energyService.GetSecondsUntilNext();
                energyTimerText.text = "Next in: " + secondsUntilNext + "s";
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
