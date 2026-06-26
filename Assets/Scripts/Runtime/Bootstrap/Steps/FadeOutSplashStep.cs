namespace Game.Runtime.Bootstrap.Steps
{
    using System.Threading;
    using Cysharp.Threading.Tasks;
    using UnityEngine;

    /// <summary>
    /// Phase 3 — fades the entire splash root to transparent, revealing
    /// the already-loaded gameplay scenes underneath. Place this step
    /// AFTER ActivateLoadedScenesStep and BEFORE UnloadBootSceneStep so
    /// the cut-over reads as a smooth dissolve instead of a hard pop.
    /// </summary>
    [CreateAssetMenu(menuName = "CoinDreams/Bootstrap/Fade Out Splash")]
    public sealed class FadeOutSplashStep : BootstrapStepAsset
    {
        [Tooltip("Seconds to fade the entire splash (all layers) to transparent.")]
        [SerializeField, Min(0f)] private float fadeSeconds = 0.5f;

        public override async UniTask ExecuteAsync(IBootContext context, CancellationToken cancellationToken)
        {
            if (context.SplashLogo == null)
            {
                return;
            }
            await context.SplashLogo.FadeOutSplashAsync(fadeSeconds, cancellationToken);
        }
    }
}
