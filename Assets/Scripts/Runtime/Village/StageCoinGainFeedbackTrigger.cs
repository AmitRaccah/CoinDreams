#nullable enable

using Game.Domain.Economy;
using Game.Infrastructure.Persistence;
using Game.Runtime.Economy;
using Game.Runtime.Player;
using MoreMountains.Feedbacks;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Runtime.Village
{
    /// <summary>
    /// Plays the stage-complete character's coin-gain Feel chain whenever the
    /// authoritative player balance increases. Listens to both direct wallet
    /// mutations and full profile swaps because server snapshots replace the
    /// wallet instance after draw/upgrade flows.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StageCoinGainFeedbackTrigger : MonoBehaviour
    {
        [Tooltip("Feel chain that plays the character coin animation.")]
        [SerializeField] private MMF_Player? coinGainFeedbacks;

        private PlayerRuntimeContext? playerRuntimeContext;
        private IPlayerSnapshotService? snapshotService;
        private ICoinPresentationGate? coinPresentationGate;
        private IReadOnlyCurrencyWallet? currencyWallet;
        private bool profileSubscribed;
        private bool walletSubscribed;
        private bool gateSubscribed;
        private bool pendingCoinGain;
        private bool hasCachedCoins;
        private int cachedCoins;

        [Inject]
        public void Construct(
            PlayerRuntimeContext context,
            IPlayerSnapshotService snapshots,
            ICoinPresentationGate gate)
        {
            if (!ReferenceEquals(playerRuntimeContext, context))
            {
                DetachFromContext(resetCoinCache: true);
                playerRuntimeContext = context;
            }

            snapshotService = snapshots;
            coinPresentationGate = gate;
            SubscribeToCoinPresentationGate();

            if (isActiveAndEnabled)
            {
                AttachToContext();
            }
        }

        private void Awake()
        {
            ResolveFeedbacks();
        }

        private void Start()
        {
            EnsureInjected();
        }

        private void OnEnable()
        {
            if (playerRuntimeContext != null)
            {
                AttachToContext();
            }
        }

        private void OnDisable()
        {
            DetachFromContext(resetCoinCache: true);
        }

        private void OnDestroy()
        {
            DetachFromContext(resetCoinCache: false);
            UnsubscribeFromCoinPresentationGate();
            coinPresentationGate = null;
            playerRuntimeContext = null;
        }

        private void AttachToContext()
        {
            SubscribeToProfileReplaced();
            RebindCurrencyWallet();
        }

        private void DetachFromContext(bool resetCoinCache)
        {
            UnsubscribeFromWallet();
            UnsubscribeFromProfileReplaced();
            currencyWallet = null;

            if (resetCoinCache)
            {
                hasCachedCoins = false;
                cachedCoins = 0;
            }
        }

        private void RebindCurrencyWallet()
        {
            IReadOnlyCurrencyWallet? nextWallet = ResolveCurrencyWallet();
            if (!ReferenceEquals(currencyWallet, nextWallet))
            {
                UnsubscribeFromWallet();
                currencyWallet = nextWallet;
            }

            if (currencyWallet == null)
            {
                return;
            }

            ObserveCoins(currencyWallet.GetCoins());
            SubscribeToWallet();
        }

        private IReadOnlyCurrencyWallet? ResolveCurrencyWallet()
        {
            if (playerRuntimeContext == null)
            {
                return null;
            }

            return playerRuntimeContext.CurrencyView;
        }

        private void SubscribeToProfileReplaced()
        {
            if (profileSubscribed || playerRuntimeContext == null)
            {
                return;
            }

            playerRuntimeContext.ProfileReplaced += HandleProfileReplaced;
            profileSubscribed = true;
        }

        private void UnsubscribeFromProfileReplaced()
        {
            if (!profileSubscribed || playerRuntimeContext == null)
            {
                return;
            }

            playerRuntimeContext.ProfileReplaced -= HandleProfileReplaced;
            profileSubscribed = false;
        }

        private void SubscribeToWallet()
        {
            if (walletSubscribed || currencyWallet == null)
            {
                return;
            }

            currencyWallet.CoinsChanged += HandleCoinsChanged;
            walletSubscribed = true;
        }

        private void UnsubscribeFromWallet()
        {
            if (!walletSubscribed || currencyWallet == null)
            {
                return;
            }

            currencyWallet.CoinsChanged -= HandleCoinsChanged;
            walletSubscribed = false;
        }

        private void HandleProfileReplaced()
        {
            RebindCurrencyWallet();
        }

        private void HandleCoinsChanged(int coins)
        {
            ObserveCoins(coins);
        }

        private void ObserveCoins(int coins)
        {
            if (!hasCachedCoins)
            {
                cachedCoins = coins;
                hasCachedCoins = true;
                return;
            }

            if (snapshotService != null && !snapshotService.LoadCompleted)
            {
                cachedCoins = coins;
                return;
            }

            bool increased = coins > cachedCoins;
            cachedCoins = coins;

            if (!increased)
            {
                return;
            }

            // While a voodoo stab animation owns the screen, defer the coin-gain
            // chain (money sound + coin anim) until the doll animation finishes —
            // otherwise it fires the instant the server commits, ahead of the
            // visual. The gate flushes us via HandleCoinPresentationReleased.
            if (coinPresentationGate != null && coinPresentationGate.IsHeld)
            {
                pendingCoinGain = true;
                return;
            }

            PlayCoinGainFeedbacks();
        }

        private void SubscribeToCoinPresentationGate()
        {
            if (gateSubscribed || coinPresentationGate == null)
            {
                return;
            }

            coinPresentationGate.Released += HandleCoinPresentationReleased;
            gateSubscribed = true;
        }

        private void UnsubscribeFromCoinPresentationGate()
        {
            if (!gateSubscribed || coinPresentationGate == null)
            {
                return;
            }

            coinPresentationGate.Released -= HandleCoinPresentationReleased;
            gateSubscribed = false;
        }

        // Gate released (stab animation done) — play the coin-gain chain now if
        // a gain was observed and withheld while the doll was mid-animation.
        private void HandleCoinPresentationReleased()
        {
            if (!pendingCoinGain)
            {
                return;
            }

            pendingCoinGain = false;
            PlayCoinGainFeedbacks();
        }

        private void PlayCoinGainFeedbacks()
        {
            ResolveFeedbacks();
            coinGainFeedbacks?.PlayFeedbacks();
        }

        private void ResolveFeedbacks()
        {
            if (coinGainFeedbacks == null)
            {
                coinGainFeedbacks = GetComponent<MMF_Player>();
            }
        }

        private void EnsureInjected()
        {
            if (playerRuntimeContext != null)
            {
                return;
            }

            LifetimeScope[] scopes = FindObjectsByType<LifetimeScope>(FindObjectsSortMode.None);
            for (int i = 0; i < scopes.Length; i++)
            {
                LifetimeScope scope = scopes[i];
                if (scope == null || scope.Container == null)
                {
                    continue;
                }

                scope.Container.Inject(this);
                if (playerRuntimeContext != null)
                {
#if UNITY_EDITOR
                    Debug.LogWarning(
                        "[StageCoinGainFeedbackTrigger] Late-injected via '" + scope.name +
                        "'. Check GameplayLifetimeScope injection timing if this repeats.",
                        this);
#endif
                    return;
                }
            }
        }
    }
}
