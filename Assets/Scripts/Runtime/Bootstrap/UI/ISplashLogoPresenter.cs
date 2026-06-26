namespace Game.Runtime.Bootstrap.UI
{
    using System.Threading;
    using Cysharp.Threading.Tasks;
    using UnityEngine;

    /// <summary>
    /// Drives the splash screen — logo overlay, the wide-image loading
    /// backdrop, the progress UI reveal, and the final fade-to-gameplay.
    /// Owned by the splash view; surfaced through <see cref="IBootContext"/>
    /// so bootstrap steps can render the intro sequence without depending
    /// on the concrete view.
    ///
    /// Phases (driven by bootstrap steps in sequence):
    ///   1. Black background + logo  → <see cref="SetLogo"/> +
    ///      <see cref="FadeInLogoAsync"/> + hold + <see cref="FadeOutLogoAsync"/>
    ///   2. Loading backdrop appears → <see cref="FadeInLoadingBackgroundAsync"/>
    ///      Progress bar + status reveal → <see cref="FadeInProgressUIAsync"/>
    ///   3. Whole splash fades into gameplay → <see cref="FadeOutSplashAsync"/>
    /// </summary>
    public interface ISplashLogoPresenter
    {
        // ---- Phase 1: logo overlay ----

        /// <summary>Snap the displayed sprite. Does not change alpha.</summary>
        void SetLogo(Sprite sprite);

        /// <summary>Fade the logo overlay's alpha from 0 → 1.</summary>
        UniTask FadeInLogoAsync(float seconds, CancellationToken cancellationToken);

        /// <summary>Fade the logo overlay's alpha from 1 → 0.</summary>
        UniTask FadeOutLogoAsync(float seconds, CancellationToken cancellationToken);

        /// <summary>Snap the logo overlay invisible without animating.</summary>
        void Hide();

        // ---- Phase 2: backdrop + progress UI ----

        /// <summary>
        /// Fade the loading background image's alpha from 0 → 1. Use this
        /// to reveal the wide intro art under the splash.
        /// </summary>
        UniTask FadeInLoadingBackgroundAsync(float seconds, CancellationToken cancellationToken);

        /// <summary>
        /// Fade the progress UI group (progress bar + status text) from
        /// 0 → 1. Call after the loading background is visible.
        /// </summary>
        UniTask FadeInProgressUIAsync(float seconds, CancellationToken cancellationToken);

        // ---- Phase 3: hand-off to gameplay ----

        /// <summary>
        /// Fade the entire splash root from 1 → 0. After this returns the
        /// underlying scenes (already loaded & active) are visible. The
        /// boot scene is typically unloaded right after.
        /// </summary>
        UniTask FadeOutSplashAsync(float seconds, CancellationToken cancellationToken);
    }
}
