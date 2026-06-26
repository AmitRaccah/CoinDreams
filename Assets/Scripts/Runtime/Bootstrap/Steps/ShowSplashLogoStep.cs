namespace Game.Runtime.Bootstrap.Steps
{
    using System.Threading;
    using Cysharp.Threading.Tasks;
    using UnityEngine;

    /// <summary>
    /// Displays a logo sprite over the splash screen for the duration of
    /// the step. Sequence: fade in → hold → fade out. Used to chain the
    /// classic "company logo → game logo → loading screen" intro by adding
    /// one of these steps per logo to <see cref="AppBootstrap.steps"/>.
    ///
    /// Skips silently if the boot context has no splash view (headless
    /// tests / dev tooling) — the step completes immediately so the boot
    /// sequence keeps moving.
    /// </summary>
    [CreateAssetMenu(menuName = "CoinDreams/Bootstrap/Show Splash Logo")]
    public sealed class ShowSplashLogoStep : BootstrapStepAsset
    {
        [Header("Logo")]
        [Tooltip("Sprite displayed during this step. The image's RectTransform " +
            "and aspect ratio live on the BootSplashView's logo Image.")]
        [SerializeField] private Sprite logo;

        [Header("Timing (seconds)")]
        [Tooltip("Fade-in duration. 0 = snap to visible.")]
        [SerializeField, Min(0f)] private float fadeInSeconds = 0.4f;

        [Tooltip("How long the logo stays fully visible between fades.")]
        [SerializeField, Min(0f)] private float holdSeconds = 1.5f;

        [Tooltip("Fade-out duration. 0 = snap to invisible.")]
        [SerializeField, Min(0f)] private float fadeOutSeconds = 0.4f;

        public override async UniTask ExecuteAsync(IBootContext context, CancellationToken cancellationToken)
        {
            if (context.SplashLogo == null)
            {
                return;
            }

            context.SplashLogo.SetLogo(logo);
            await context.SplashLogo.FadeInLogoAsync(fadeInSeconds, cancellationToken);
            if (holdSeconds > 0f)
            {
                await UniTask.Delay(
                    System.TimeSpan.FromSeconds(holdSeconds),
                    DelayType.UnscaledDeltaTime,
                    PlayerLoopTiming.Update,
                    cancellationToken);
            }
            await context.SplashLogo.FadeOutLogoAsync(fadeOutSeconds, cancellationToken);
        }
    }
}
