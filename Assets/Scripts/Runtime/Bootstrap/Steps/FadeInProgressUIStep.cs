namespace Game.Runtime.Bootstrap.Steps
{
    using System.Threading;
    using Cysharp.Threading.Tasks;
    using UnityEngine;

    /// <summary>
    /// Phase 2 — reveals the progress bar + status text on top of the
    /// loading backdrop. Use after FadeInLoadingBackgroundStep so the bar
    /// appears against the wide art rather than the black background.
    /// </summary>
    [CreateAssetMenu(menuName = "CoinDreams/Bootstrap/Fade In Progress UI")]
    public sealed class FadeInProgressUIStep : BootstrapStepAsset
    {
        [Tooltip("Seconds to fade the progress bar + status text from invisible to fully visible.")]
        [SerializeField, Min(0f)] private float fadeSeconds = 0.3f;

        public override async UniTask ExecuteAsync(IBootContext context, CancellationToken cancellationToken)
        {
            if (context.SplashLogo == null)
            {
                return;
            }
            await context.SplashLogo.FadeInProgressUIAsync(fadeSeconds, cancellationToken);
        }
    }
}
