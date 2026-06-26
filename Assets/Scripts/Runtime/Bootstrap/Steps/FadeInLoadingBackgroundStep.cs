namespace Game.Runtime.Bootstrap.Steps
{
    using System.Threading;
    using Cysharp.Threading.Tasks;
    using UnityEngine;

    /// <summary>
    /// Phase 2 — fades the wide intro art (loading backdrop) in over the
    /// black background. Place this step right after the logo show step
    /// to transition from "logo on black" to "art with progress UI".
    /// </summary>
    [CreateAssetMenu(menuName = "CoinDreams/Bootstrap/Fade In Loading Background")]
    public sealed class FadeInLoadingBackgroundStep : BootstrapStepAsset
    {
        [Tooltip("Seconds to fade the loading background from invisible to fully visible.")]
        [SerializeField, Min(0f)] private float fadeSeconds = 0.4f;

        public override async UniTask ExecuteAsync(IBootContext context, CancellationToken cancellationToken)
        {
            if (context.SplashLogo == null)
            {
                return;
            }
            await context.SplashLogo.FadeInLoadingBackgroundAsync(fadeSeconds, cancellationToken);
        }
    }
}
