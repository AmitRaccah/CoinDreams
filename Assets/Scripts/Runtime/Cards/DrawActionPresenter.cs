#nullable enable
using System.Threading.Tasks;
using Game.Config.Cards;
using Game.Domain.Cards;
using Game.Runtime.Player;
using UnityEngine;
using VContainer;

namespace Game.Runtime.Cards
{
    [DisallowMultipleComponent]
    public sealed class DrawActionPresenter : MonoBehaviour, IDrawGameActions
    {
        [Header("Config")]
        [SerializeField] private int drawCost = 1;
        [SerializeField] private CardDeckSO? deckConfig;

        [Inject] private PlayerRuntimeContext? playerRuntimeContext;
        [Inject] private IAuthoritativeDrawService? authoritativeDrawService;

        private IDrawResultSink? drawResultSink;
        private AuthoritativeDrawRequest? drawRequest;
        private bool isDrawInFlight;
        private int pendingMultiplier = 1;
        private string? pendingDrawId;
        private readonly AuthoritativeDrawRequestFactory drawRequestFactory =
            new AuthoritativeDrawRequestFactory();

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
                // Clear so the next workflow attempt mints a fresh drawId.
                pendingDrawId = null;
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
            if (string.IsNullOrEmpty(pendingDrawId))
            {
                pendingDrawId = System.Guid.NewGuid().ToString("N");
            }

            drawRequest = drawRequestFactory.Create(drawCost, pendingMultiplier, deckConfig, pendingDrawId);
        }

        private void ResolveDrawResultSink()
        {
            if (drawResultSink != null)
            {
                return;
            }

            drawResultSink = GetComponent<IDrawResultSink>();
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
