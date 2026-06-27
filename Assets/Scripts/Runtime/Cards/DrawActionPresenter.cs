#nullable enable
using System;
using System.Threading.Tasks;
using Game.Signals;
using Game.Config.Cards;
using Game.Domain.Cards;
using Game.Runtime.Player;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace Game.Runtime.Cards
{
    [DisallowMultipleComponent]
    public sealed class DrawActionPresenter : MonoBehaviour, IDrawGameActions
    {
        [Header("Config")]
        [SerializeField] private CardDeckSO? deckConfig;

        [Inject] private PlayerRuntimeContext? playerRuntimeContext;
        [Inject] private IAuthoritativeDrawService? authoritativeDrawService;
        [Inject] private CardDrawConfigSO? cardDrawConfig;
        [Inject] private ISubscriber<MultiplierChangeRequestedSignal>? multiplierSubscriber;
        // Steal launcher injection removed — StealCardEffect now owns the
        // steal-card trigger path end-to-end (BeginVoodooSession during the
        // animation, session-started signal at the end of the animation).

        private IDrawResultSink? drawResultSink;
        private AuthoritativeDrawRequest? drawRequest;
        private IDisposable? multiplierSubscription;
        private bool isDrawInFlight;
        private int pendingMultiplier = 1;
        private string? pendingDrawId;
        private readonly AuthoritativeDrawRequestFactory drawRequestFactory =
            new AuthoritativeDrawRequestFactory();

        private void OnEnable()
        {
            if (multiplierSubscriber != null && multiplierSubscription == null)
            {
                multiplierSubscription = multiplierSubscriber.Subscribe(HandleMultiplierChangeRequested);
            }
        }

        private void OnDisable()
        {
            multiplierSubscription?.Dispose();
            multiplierSubscription = null;
        }

        private void HandleMultiplierChangeRequested(MultiplierChangeRequestedSignal signal)
        {
            SetMultiplier(signal.Multiplier);
        }

        public void SetMultiplier(int multiplier)
        {
            if (System.Array.IndexOf(AuthoritativeDrawRequest.AllowedMultipliers, multiplier) < 0)
            {
                return;
            }

            pendingMultiplier = multiplier;

            if (!isDrawInFlight)
            {
                RebuildDrawRequest();
            }
        }

        public async Task<CardDrawContext> TryDrawAsync()
        {
            // Capture the multiplier in effect at the moment the draw is
            // attempted. Threaded through CardDrawContext so a UI change
            // mid-flight can't race with the steal effect's prepared session.
            int multiplierAtDraw = pendingMultiplier;

            // Preconditions ARE published immediately — there's no card
            // animation to wait for when the draw can't even start, and
            // the HUD needs to show the "syncing..." / "deck invalid"
            // message right away. Only the SUCCESS path is gated; the
            // executor calls PublishResult once the visual lock releases.
            if (!TryPrepareDraw(out AuthoritativeDrawResult? preconditionFailure))
            {
                PublishResult(preconditionFailure!);
                return new CardDrawContext(preconditionFailure!, multiplierAtDraw);
            }

            isDrawInFlight = true;
            try
            {
                AuthoritativeDrawResult result = await authoritativeDrawService!.TryDrawAsync(drawRequest!);
                if (result == null)
                {
                    result = AuthoritativeDrawResult.Error("Draw failed.");
                }

                return new CardDrawContext(result, multiplierAtDraw);
            }
            catch (System.Exception exception)
            {
                AuthoritativeDrawResult errorResult = AuthoritativeDrawResult.Error("Draw failed.");
                Debug.LogError("[DrawActionPresenter] Failed: " + exception.Message, this);
                return new CardDrawContext(errorResult, multiplierAtDraw);
            }
            finally
            {
                isDrawInFlight = false;
                pendingDrawId = null;
                drawRequest = null;
            }
        }

        public CardDrawContext? TryRejectUnaffordableDraw()
        {
            // No player/config yet → let TryDrawAsync's precondition path
            // surface the failure instead of guessing affordability here.
            if (playerRuntimeContext == null || cardDrawConfig == null)
            {
                return null;
            }

            // Same price the engine will charge (single source of truth), so the
            // gate and the spend can never disagree on cost.
            int cost = AuthoritativeDrawRequest.ScaleDrawCost(cardDrawConfig.DrawCost, pendingMultiplier);
            if (playerRuntimeContext.EnergyView.GetCurrent() >= cost)
            {
                return null;
            }

            return new CardDrawContext(
                AuthoritativeDrawResult.NotEnoughEnergy(playerRuntimeContext.CreateSnapshot()),
                pendingMultiplier);
        }

        public void PublishResult(AuthoritativeDrawResult result)
        {
            if (result == null) return;
            ResolveDrawResultSink();

            if (drawResultSink == null)
            {
                return;
            }

            drawResultSink.Present(result);
        }

        private bool TryPrepareDraw(out AuthoritativeDrawResult? failureResult)
        {
            failureResult = null;

            if (isDrawInFlight)
            {
                failureResult = AuthoritativeDrawResult.Unavailable("Draw is already in progress.");
                return false;
            }

            if (playerRuntimeContext == null)
            {
                failureResult = AuthoritativeDrawResult.Unavailable("Player context missing.");
                return false;
            }

            if (authoritativeDrawService == null)
            {
                failureResult = AuthoritativeDrawResult.Unavailable("Draw service missing.");
                return false;
            }

            if (cardDrawConfig == null)
            {
                failureResult = AuthoritativeDrawResult.Unavailable("Draw config missing.");
                return false;
            }

            if (!authoritativeDrawService.IsReady)
            {
                failureResult = AuthoritativeDrawResult.Unavailable("Syncing player state...");
                return false;
            }

            if (drawRequest == null)
            {
                RebuildDrawRequest();
            }

            if (drawRequest == null)
            {
                failureResult = AuthoritativeDrawResult.Invalid("Draw deck is invalid.");
                return false;
            }

            ResolveDrawResultSink();
            return true;
        }

        private void RebuildDrawRequest()
        {
            if (cardDrawConfig == null)
            {
                drawRequest = null;
                return;
            }

            if (string.IsNullOrEmpty(pendingDrawId))
            {
                pendingDrawId = System.Guid.NewGuid().ToString("N");
            }

            drawRequest = drawRequestFactory.Create(
                cardDrawConfig.DrawCost,
                pendingMultiplier,
                deckConfig,
                pendingDrawId);
        }

        private void ResolveDrawResultSink()
        {
            if (drawResultSink != null)
            {
                return;
            }

            drawResultSink = GetComponent<IDrawResultSink>();
        }
    }
}
