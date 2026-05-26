namespace Game.Runtime.Bootstrap.UI
{
    using TMPro;
    using UnityEngine;
    using UnityEngine.UI;

    [DisallowMultipleComponent]
    public sealed class BootSplashView : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private Image progressBar;
        [SerializeField] private TMP_Text statusText;

        [Header("Style")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color errorColor = Color.red;

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
    }
}
