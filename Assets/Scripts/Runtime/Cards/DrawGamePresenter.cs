using System.Collections;
using Game.Domain.Cards;
using Game.Config.Cards;
using Game.Domain.Economy;
using Game.Domain.Energy;
using Game.Domain.Minigames;
using Game.Runtime.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.Cards
{
    public sealed class DrawGamePresenter : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private int drawCost = 1;
        [SerializeField] private CardDeckSO deckConfig;
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
        private DrawCardUseCase drawCardUseCase;
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

        private void Awake()
        {
            if (!TryResolvePlayerContext())
            {
                Debug.LogError("[DrawGamePresenter] Missing PlayerRuntimeContext. Assign PlayerRuntimeContext to use a single player source of truth.", this);
                enabled = false;
                return;
            }

            energyService = playerRuntimeContext.EnergyService;
            currencyService = playerRuntimeContext.CurrencyService;

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

            energyService.ApplyRegen();
            RefreshAllUi();
        }

        private void OnEnable()
        {
            SubscribeToStateEvents();
            StartUiRefreshLoop();
            RefreshAllUi();
        }

        private void OnDisable()
        {
            StopUiRefreshLoop();
            UnsubscribeFromStateEvents();
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
                Debug.LogWarning("[DRAW] Failed: not enough energy.");
                return;
            }

            SetResult("Card: " + drawnCard.Id);
            Debug.Log("[DRAW] Card=" + drawnCard.Id + " | Coins=" + currencyService.GetCoins());
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
            if (pause == false && energyService != null)
            {
                energyService.ApplyRegen();
                RefreshAllUi();
            }
        }
    }
}
