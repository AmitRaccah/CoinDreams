using System.Threading.Tasks;
using Game.Config.Cards;
using Game.Domain.Cards;
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
            drawRequest = drawRequestFactory.Create(drawCost, deckConfig);
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
            authoritativeDrawService = null;

            if (authoritativeDrawServiceSource != null)
            {
                authoritativeDrawService = authoritativeDrawServiceSource as IAuthoritativeDrawService;
                if (authoritativeDrawService == null)
                {
                    Debug.LogError(
                        "[DrawActionPresenter] Configured authoritative draw source does not implement IAuthoritativeDrawService.",
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
                "[DrawActionPresenter] No IAuthoritativeDrawService implementation found in scene.",
                this);
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
