#nullable enable
using System;
using System.Threading.Tasks;
using Game.Composition.Signals;
using Game.Config.Cards;
using Game.Domain.Cards;
using Game.Domain.Steal;
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
        [Inject] private IStealCardLauncher? stealCardLauncher;

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

        public async Task<AuthoritativeDrawResult> TryDrawAsync()
        {
            if (!TryPrepareDraw(out AuthoritativeDrawResult? preconditionFailure))
            {
                PublishResult(preconditionFailure!);
                return preconditionFailure!;
            }

            isDrawInFlight = true;
            try
            {
                AuthoritativeDrawResult result = await authoritativeDrawService!.TryDrawAsync(drawRequest!);
                if (result == null)
                {
                    result = AuthoritativeDrawResult.Error("Draw failed.");
                }

                PublishResult(result);
                FireStealLauncherIfNeeded(result);
                return result;
            }
            catch (System.Exception exception)
            {
                AuthoritativeDrawResult errorResult = AuthoritativeDrawResult.Error("Draw failed.");
                PublishResult(errorResult);
                Debug.LogError("[DrawActionPresenter] Failed: " + exception.Message, this);
                return errorResult;
            }
            finally
            {
                isDrawInFlight = false;
                pendingDrawId = null;
                drawRequest = null;
            }
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

        // Bridges the engine's StealTriggerId into the live IStealCardLauncher.
        // The engine itself runs against a no-op CapturingStealCardLauncher so it
        // can return a pure result; the side effect (publishing the trigger
        // signal that opens a voodoo session) lives here, on the client side.
        private void FireStealLauncherIfNeeded(AuthoritativeDrawResult result)
        {
            if (result == null || !result.IsSuccess) return;
            if (string.IsNullOrEmpty(result.StealTriggerId)) return;
            if (stealCardLauncher == null)
            {
                Debug.LogWarning("[DrawActionPresenter] StealCardLauncher is not injected — voodoo session will not start.", this);
                return;
            }
            stealCardLauncher.Launch(result.StealTriggerId, pendingMultiplier);
        }

        private void PublishResult(AuthoritativeDrawResult result)
        {
            ResolveDrawResultSink();

            if (drawResultSink == null)
            {
                return;
            }

            drawResultSink.Present(result);
        }
    }
}
