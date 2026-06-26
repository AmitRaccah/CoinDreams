namespace Game.Runtime.Bootstrap.UI
{
    using System.Threading;
    using Cysharp.Threading.Tasks;
    using TMPro;
    using UnityEngine;
    using UnityEngine.UI;

    [DisallowMultipleComponent]
    public sealed class BootSplashView : MonoBehaviour, ISplashLogoPresenter
    {
        [Header("Progress UI")]
        [SerializeField] private Image progressBar;
        [SerializeField] private TMP_Text statusText;
        [Tooltip("CanvasGroup wrapping the progress bar and status text. " +
            "Faded in once the loading backdrop is visible (phase 2).")]
        [SerializeField] private CanvasGroup progressUIGroup;

        [Header("Backdrop Layers")]
        [Tooltip("Solid-colour background that fills the whole splash for " +
            "phase 1 (typically opaque black). Always at alpha 1.")]
        [SerializeField] private Image blackBackground;
        [Tooltip("Wide intro art shown during phase 2. The CanvasGroup " +
            "below drives its fade-in.")]
        [SerializeField] private Image loadingBackground;
        [SerializeField] private CanvasGroup loadingBackgroundCanvasGroup;

        [Header("Logo Overlay (Phase 1)")]
        [Tooltip("Full-screen Image rendered above the backdrop. " +
            "ShowSplashLogoStep swaps its sprite per logo in the sequence.")]
        [SerializeField] private Image logoImage;
        [Tooltip("CanvasGroup that drives the logo's fade in/out. Lives on " +
            "the same GameObject as logoImage (or a parent).")]
        [SerializeField] private CanvasGroup logoCanvasGroup;

        [Header("Splash Root (Phase 3)")]
        [Tooltip("CanvasGroup on the splash root. Faded to 0 by " +
            "FadeOutSplashStep right before the boot scene unloads, " +
            "revealing the gameplay scene below.")]
        [SerializeField] private CanvasGroup rootCanvasGroup;

        [Header("Style")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color errorColor = Color.red;

        private void Awake()
        {
            // Set explicit initial state — overlay invisible, progress UI
            // invisible, loading backdrop invisible. Only the black
            // background is visible at boot start. The splash root stays
            // at alpha 1 until phase 3.
            if (logoCanvasGroup != null) logoCanvasGroup.alpha = 0f;
            if (loadingBackgroundCanvasGroup != null) loadingBackgroundCanvasGroup.alpha = 0f;
            if (progressUIGroup != null) progressUIGroup.alpha = 0f;
            if (rootCanvasGroup != null) rootCanvasGroup.alpha = 1f;
        }

        public void SetProgress(float t)
        {
            if (progressBar == null)
            {
                return;
            }

            progressBar.fillAmount = Mathf.Clamp01(t);
        }

        public void SetStatus(string text)
        {
            if (statusText == null)
            {
                return;
            }

            statusText.color = normalColor;
            statusText.text = text;
        }

        public void ShowError(string message)
        {
            if (statusText == null)
            {
                return;
            }

            statusText.color = errorColor;
            statusText.text = "Error: " + message;
        }

        public void SetLogo(Sprite sprite)
        {
            if (logoImage == null)
            {
                return;
            }

            logoImage.sprite = sprite;
            logoImage.enabled = sprite != null;
        }

        public UniTask FadeInLogoAsync(float seconds, CancellationToken cancellationToken)
            => FadeAsync(logoCanvasGroup, 0f, 1f, seconds, cancellationToken);

        public UniTask FadeOutLogoAsync(float seconds, CancellationToken cancellationToken)
            => FadeAsync(logoCanvasGroup, 1f, 0f, seconds, cancellationToken);

        public void Hide()
        {
            if (logoCanvasGroup == null)
            {
                return;
            }
            logoCanvasGroup.alpha = 0f;
        }

        public UniTask FadeInLoadingBackgroundAsync(float seconds, CancellationToken cancellationToken)
            => FadeAsync(loadingBackgroundCanvasGroup, 0f, 1f, seconds, cancellationToken);

        public UniTask FadeInProgressUIAsync(float seconds, CancellationToken cancellationToken)
            => FadeAsync(progressUIGroup, 0f, 1f, seconds, cancellationToken);

        public UniTask FadeOutSplashAsync(float seconds, CancellationToken cancellationToken)
            => FadeAsync(rootCanvasGroup, 1f, 0f, seconds, cancellationToken);

        // Single fade primitive — every named method above delegates here.
        // Null-safe so a missing field on a partial setup doesn't crash the
        // boot sequence; instead the step becomes a no-op.
        private static async UniTask FadeAsync(
            CanvasGroup group,
            float from,
            float to,
            float seconds,
            CancellationToken cancellationToken)
        {
            if (group == null)
            {
                return;
            }

            if (seconds <= 0f)
            {
                group.alpha = to;
                return;
            }

            float elapsed = 0f;
            group.alpha = from;
            while (elapsed < seconds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / seconds));
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }
            group.alpha = to;
        }
    }
}
