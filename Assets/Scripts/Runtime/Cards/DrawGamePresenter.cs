using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Game.Domain.Cards;
using Game.Config.Cards;
using Game.Domain.Economy;
using Game.Domain.Energy;
using Game.Runtime.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.Cards
{
    public sealed class DrawGamePresenter : MonoBehaviour, IDrawGameActions
    {
        private const int FallbackWeight = 1;
        private const int FallbackCoinsAmount = 100;
        private const string FallbackCardId = "fallback_add_coins";

        [Header("Config")]
        [SerializeField] private int drawCost = 1;
        [SerializeField] private CardDeckSO deckConfig;
        [SerializeField] private float uiRefreshIntervalSeconds = 1f;
        [SerializeField] private PlayerRuntimeContext playerRuntimeContext;
        [SerializeField] private MonoBehaviour authoritativeDrawServiceSource;

        [Header("UI")]
        [SerializeField] private Slider energySlider;
        [SerializeField] private TMP_Text energyText;
        [SerializeField] private TMP_Text energyTimerText;
        [SerializeField] private TMP_Text extraEnergyText;
        [SerializeField] private TMP_Text coinsText;
        [SerializeField] private TMP_Text resultText;

        private EnergyService energyService;
        private CurrencyService currencyService;
        private IAuthoritativeDrawService authoritativeDrawService;
        private AuthoritativeDrawRequest drawRequest;
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
        private bool isDrawInFlight;

        private void Awake()
        {
            if (!TryResolvePlayerContext())
            {
                Debug.LogError("[DrawGamePresenter] Missing PlayerRuntimeContext. Assign PlayerRuntimeContext to use a single player source of truth.", this);
                enabled = false;
                return;
            }

            ResolveAuthoritativeDrawService();
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

        public async Task<AuthoritativeDrawResult> TryDrawAsync()
        {
            if (!TryPrepareDraw(out AuthoritativeDrawResult preconditionFailure))
            {
                ApplyDrawResult(preconditionFailure);
                return preconditionFailure;
            }

            isDrawInFlight = true;
            try
            {
                AuthoritativeDrawResult result = await authoritativeDrawService.TryDrawAsync(drawRequest);
                if (result == null)
                {
                    result = AuthoritativeDrawResult.Error("Draw failed.");
                }

                ApplyDrawResult(result);
                RefreshAllUi();
                return result;
            }
            catch (System.Exception exception)
            {
                AuthoritativeDrawResult errorResult = AuthoritativeDrawResult.Error("Draw failed.");
                ApplyDrawResult(errorResult);
                Debug.LogError("[DRAW] Failed: " + exception.Message, this);
                return errorResult;
            }
            finally
            {
                isDrawInFlight = false;
            }
        }

        private bool TryPrepareDraw(out AuthoritativeDrawResult failureResult)
        {
            failureResult = null;

            if (isDrawInFlight)
            {
                failureResult = AuthoritativeDrawResult.Unavailable("Draw is already in progress.");
                return false;
            }

            if (authoritativeDrawService == null)
            {
                ResolveAuthoritativeDrawService();
            }

            if (authoritativeDrawService == null)
            {
                failureResult = AuthoritativeDrawResult.Unavailable("Draw service missing.");
                return false;
            }

            if (!authoritativeDrawService.IsReady)
            {
                failureResult = AuthoritativeDrawResult.Unavailable("Syncing player state...");
                return false;
            }

            if (drawRequest == null)
            {
                failureResult = AuthoritativeDrawResult.Invalid("Draw deck is invalid.");
                return false;
            }

            return true;
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
                drawRequest = null;
                return;
            }

            energyService = playerRuntimeContext.EnergyService;
            currencyService = playerRuntimeContext.CurrencyService;
            drawRequest = BuildDrawRequest();

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
            if (pause == false && energyService != null)
            {
                energyService.ApplyRegen();
                RefreshAllUi();
            }
        }

        private void ResolveAuthoritativeDrawService()
        {
            authoritativeDrawService = null;

            if (authoritativeDrawServiceSource != null)
            {
                authoritativeDrawService = authoritativeDrawServiceSource as IAuthoritativeDrawService;
                if (authoritativeDrawService == null)
                {
                    Debug.LogError(
                        "[DrawGamePresenter] Configured authoritative draw source does not implement IAuthoritativeDrawService.",
                        this);
                }
            }

            if (authoritativeDrawService != null)
            {
                return;
            }

            MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            int i;
            for (i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                IAuthoritativeDrawService drawService = behaviour as IAuthoritativeDrawService;
                if (drawService == null)
                {
                    continue;
                }

                authoritativeDrawService = drawService;
                authoritativeDrawServiceSource = behaviour;
                return;
            }

            Debug.LogWarning(
                "[DrawGamePresenter] No IAuthoritativeDrawService implementation found in scene.",
                this);
        }

        private AuthoritativeDrawRequest BuildDrawRequest()
        {
            List<AuthoritativeDrawCardDefinition> cards = new List<AuthoritativeDrawCardDefinition>();

            if (deckConfig != null && deckConfig.Cards != null)
            {
                int i;
                for (i = 0; i < deckConfig.Cards.Count; i++)
                {
                    CardDefinitionSO card = deckConfig.Cards[i];
                    if (card == null || string.IsNullOrWhiteSpace(card.CardId))
                    {
                        continue;
                    }

                    int weight = card.Weight;
                    if (weight <= 0)
                    {
                        weight = FallbackWeight;
                    }

                    AuthoritativeDrawEffectDefinition[] effects =
                        BuildEffects(card.EffectConfigs);

                    cards.Add(new AuthoritativeDrawCardDefinition(
                        card.CardId.Trim(),
                        weight,
                        effects));
                }
            }

            if (cards.Count == 0)
            {
                cards.Add(CreateFallbackCard());
            }

            return new AuthoritativeDrawRequest(drawCost, cards.ToArray());
        }

        private static AuthoritativeDrawCardDefinition CreateFallbackCard()
        {
            AuthoritativeDrawEffectDefinition[] effects = new AuthoritativeDrawEffectDefinition[1];
            effects[0] = new AuthoritativeDrawEffectDefinition(
                AuthoritativeDrawEffectType.AddCoins,
                FallbackCoinsAmount,
                string.Empty);

            return new AuthoritativeDrawCardDefinition(FallbackCardId, FallbackWeight, effects);
        }

        private static AuthoritativeDrawEffectDefinition[] BuildEffects(
            List<RewardEffectConfig> effectConfigs)
        {
            if (effectConfigs == null || effectConfigs.Count == 0)
            {
                return System.Array.Empty<AuthoritativeDrawEffectDefinition>();
            }

            List<AuthoritativeDrawEffectDefinition> effects =
                new List<AuthoritativeDrawEffectDefinition>(effectConfigs.Count);

            int i;
            for (i = 0; i < effectConfigs.Count; i++)
            {
                RewardEffectConfig config = effectConfigs[i];
                if (config == null)
                {
                    continue;
                }

                if (!TryMapEffectType(config.EffectType, out AuthoritativeDrawEffectType mappedType))
                {
                    continue;
                }

                effects.Add(new AuthoritativeDrawEffectDefinition(
                    mappedType,
                    config.IntValue,
                    config.StringValue));
            }

            if (effects.Count == 0)
            {
                return System.Array.Empty<AuthoritativeDrawEffectDefinition>();
            }

            return effects.ToArray();
        }

        private static bool TryMapEffectType(
            RewardEffectType sourceType,
            out AuthoritativeDrawEffectType mappedType)
        {
            mappedType = AuthoritativeDrawEffectType.AddCoins;

            if (sourceType == RewardEffectType.AddCoins)
            {
                mappedType = AuthoritativeDrawEffectType.AddCoins;
                return true;
            }

            if (sourceType == RewardEffectType.AddEnergy)
            {
                mappedType = AuthoritativeDrawEffectType.AddEnergy;
                return true;
            }

            if (sourceType == RewardEffectType.LaunchMinigame)
            {
                mappedType = AuthoritativeDrawEffectType.LaunchMinigame;
                return true;
            }

            if (sourceType == RewardEffectType.DoubleNextDraw)
            {
                mappedType = AuthoritativeDrawEffectType.DoubleNextDraw;
                return true;
            }

            return false;
        }

        private void ApplyDrawResult(AuthoritativeDrawResult result)
        {
            if (result == null)
            {
                SetResult("Draw failed.");
                return;
            }

            if (result.Status == AuthoritativeDrawStatus.Success)
            {
                string message = "Card: " + result.DrawnCardId;
                if (!string.IsNullOrEmpty(result.MinigameId))
                {
                    message += " | Minigame: " + result.MinigameId;
                }

                SetResult(message);
                return;
            }

            if (result.Status == AuthoritativeDrawStatus.NotEnoughEnergy)
            {
                SetResult("Not enough energy.");
                return;
            }

            if (result.Status == AuthoritativeDrawStatus.DeckEmpty)
            {
                SetResult("Deck is empty.");
                return;
            }

            if (!string.IsNullOrEmpty(result.Message))
            {
                SetResult(result.Message);
                return;
            }

            SetResult("Draw failed.");
        }
    }
}
