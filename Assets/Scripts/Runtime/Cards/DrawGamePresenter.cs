using System.Threading.Tasks;
using Game.Config.Cards;
using Game.Domain.Cards;
using Game.Runtime.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.Cards
{
    [DisallowMultipleComponent]
    public sealed class DrawGamePresenter : MonoBehaviour, IDrawGameActions
    {
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

        private DrawActionPresenter drawActionPresenter;
        private DrawHudPresenter drawHudPresenter;
        private IDrawGameActions drawActions;

        private void Awake()
        {
            EnsurePresenters();
            BindPresenters();
        }

        private void OnEnable()
        {
            EnsurePresenters();
            BindPresenters();
        }

        public Task<AuthoritativeDrawResult> TryDrawAsync()
        {
            if (drawActions == null)
            {
                return Task.FromResult(AuthoritativeDrawResult.Unavailable("Draw actions are not available."));
            }

            return drawActions.TryDrawAsync();
        }

        private void EnsurePresenters()
        {
            if (drawActionPresenter == null)
            {
                drawActionPresenter = GetComponent<DrawActionPresenter>();
            }

            if (drawActionPresenter == null)
            {
                drawActionPresenter = gameObject.AddComponent<DrawActionPresenter>();
            }

            if (drawHudPresenter == null)
            {
                drawHudPresenter = GetComponent<DrawHudPresenter>();
            }

            if (drawHudPresenter == null)
            {
                drawHudPresenter = gameObject.AddComponent<DrawHudPresenter>();
            }

            drawActions = drawActionPresenter;
        }

        private void BindPresenters()
        {
            if (drawActionPresenter == null || drawHudPresenter == null)
            {
                return;
            }

            drawHudPresenter.Configure(
                playerRuntimeContext,
                uiRefreshIntervalSeconds,
                energySlider,
                energyText,
                energyTimerText,
                extraEnergyText,
                coinsText,
                resultText);

            drawActionPresenter.Configure(
                drawCost,
                deckConfig,
                playerRuntimeContext,
                authoritativeDrawServiceSource,
                drawHudPresenter);
        }
    }
}
