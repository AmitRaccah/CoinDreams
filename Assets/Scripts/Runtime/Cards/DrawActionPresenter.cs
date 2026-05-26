using System.Threading.Tasks;
using Game.Config.Cards;
using Game.Domain.Cards;
using Game.Runtime;
using Game.Runtime.Player;
using UnityEngine;

namespace Game.Runtime.Cards
{
    [DisallowMultipleComponent]
    public sealed class DrawActionPresenter : MonoBehaviour, IDrawGameActions
    {
        [Header("Config")]
        [SerializeField] private int drawCost = 1;
        [SerializeField] private CardDeckSO deckConfig;
        [SerializeField] private PlayerRuntimeContext playerRuntimeContext;
        [SerializeField] private MonoBehaviour authoritativeDrawServiceSource;
        [SerializeField] private MonoBehaviour drawResultSinkSource;

        private IAuthoritativeDrawService authoritativeDrawService;
        private IDrawResultSink drawResultSink;
        private AuthoritativeDrawRequest drawRequest;
        private bool isDrawInFlight;
        private int pendingMultiplier = 1;
        private string pendingDrawId;
        private readonly AuthoritativeDrawRequestFactory drawRequestFactory =
            new AuthoritativeDrawRequestFactory();

        public void Configure(
            int drawCost,
            CardDeckSO deckConfig,
            PlayerRuntimeContext playerRuntimeContext,
            MonoBehaviour authoritativeDrawServiceSource,
            IDrawResultSink drawResultSink)
        {
            this.drawCost = drawCost;
            this.deckConfig = deckConfig;
            this.playerRuntimeContext = playerRuntimeContext;
            this.authoritativeDrawServiceSource = authoritativeDrawServiceSource;
            this.drawResultSink = drawResultSink;
            drawResultSinkSource = drawResultSink as MonoBehaviour;
            RebuildDrawRequest();
            ResolveAuthoritativeDrawService();
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
            if (!TryPrepareDraw(out AuthoritativeDrawResult preconditionFailure))
            {
                PublishResult(preconditionFailure);
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

        private bool TryPrepareDraw(out AuthoritativeDrawResult failureResult)
        {
            failureResult = null;

            if (isDrawInFlight)
            {
                failureResult = AuthoritativeDrawResult.Unavailable("Draw is already in progress.");
                return false;
            }

            if (!TryResolvePlayerContext())
            {
                failureResult = AuthoritativeDrawResult.Unavailable("Player context missing.");
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

            if (drawResultSinkSource != null)
            {
                drawResultSink = drawResultSinkSource as IDrawResultSink;
            }

            if (drawResultSink != null)
            {
                return;
            }

            drawResultSink = GetComponent<IDrawResultSink>();
        }

        private void ResolveAuthoritativeDrawService()
        {
            if (RuntimeServiceResolver.TryResolveAuthoritativeDrawService(
                    authoritativeDrawServiceSource,
                    out authoritativeDrawService,
                    out MonoBehaviour resolvedSource))
            {
                authoritativeDrawServiceSource = resolvedSource;
                return;
            }

            Debug.LogWarning(
                "[DrawActionPresenter] No IAuthoritativeDrawService implementation found in scene.",
                this);
        }

        private bool TryResolvePlayerContext()
        {
            return RuntimeServiceResolver.TryResolvePlayerContext(
                playerRuntimeContext,
                out playerRuntimeContext);
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
