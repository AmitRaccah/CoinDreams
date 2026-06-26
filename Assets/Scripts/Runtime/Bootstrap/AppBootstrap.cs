namespace Game.Runtime.Bootstrap
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using Cysharp.Threading.Tasks;
    using Game.Runtime.Bootstrap.UI;
    using Game.Runtime.Scenes;
    using UnityEngine;

    [DisallowMultipleComponent]
    public sealed class AppBootstrap : MonoBehaviour
    {
        [Header("Splash UI")]
        [SerializeField] private BootSplashView splashView;

        [Header("Step Sequence (run in order)")]
        [SerializeField] private List<BootstrapStepAsset> steps = new List<BootstrapStepAsset>();

        private CancellationTokenSource cts;

        private async void Start()
        {
            cts = new CancellationTokenSource();
            try
            {
                await RunBootSequenceAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, this);
                if (splashView != null)
                {
                    splashView.ShowError(ex.Message);
                }
            }
        }

        private void OnDestroy()
        {
            if (cts != null)
            {
                cts.Cancel();
                cts.Dispose();
                cts = null;
            }
        }

        private async UniTask RunBootSequenceAsync(CancellationToken ct)
        {
            ISceneLoader loader = new UnitySceneLoader();
            // splashView IS the ISplashLogoPresenter implementation. Passing
            // it through the context lets bootstrap steps render logos
            // without coupling to the concrete view.
            BootContext context = new BootContext(loader, null, SetStatusOnSplash, splashView);

            float totalWeight = 0f;
            int i;
            for (i = 0; i < steps.Count; i++)
            {
                BootstrapStepAsset step = steps[i];
                if (step == null)
                {
                    continue;
                }

                totalWeight += step.Weight;
            }

            if (totalWeight <= 0f)
            {
                totalWeight = 1f;
            }

            float accumulated = 0f;
            for (i = 0; i < steps.Count; i++)
            {
                BootstrapStepAsset step = steps[i];
                if (step == null)
                {
                    continue;
                }

                ct.ThrowIfCancellationRequested();

                if (splashView != null)
                {
                    splashView.SetStatus(step.DisplayName);
                }

                float capturedAccumulated = accumulated;
                float capturedWeight = step.Weight;
                float capturedTotal = totalWeight;
                Progress<float> stepProgress = new Progress<float>(p =>
                {
                    if (splashView == null)
                    {
                        return;
                    }

                    float t = (capturedAccumulated + Mathf.Clamp01(p) * capturedWeight) / capturedTotal;
                    splashView.SetProgress(t);
                });
                context.SetStepProgress(stepProgress);

                await step.ExecuteAsync(context, ct);

                accumulated += step.Weight;

                if (splashView != null)
                {
                    splashView.SetProgress(accumulated / totalWeight);
                }
            }

            if (splashView != null)
            {
                splashView.SetStatus("Ready");
            }
        }

        private void SetStatusOnSplash(string text)
        {
            if (splashView == null)
            {
                return;
            }

            splashView.SetStatus(text);
        }
    }
}
