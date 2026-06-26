namespace Game.Runtime.Bootstrap
{
    using System;
    using Game.Runtime.Bootstrap.UI;
    using Game.Runtime.Scenes;

    public sealed class BootContext : IBootContext
    {
        private readonly ISceneLoader sceneLoader;
        private readonly Action<string> setStatusText;
        private readonly ISplashLogoPresenter splashLogo;
        private IProgress<float> stepProgress;

        public BootContext(
            ISceneLoader sceneLoader,
            IProgress<float> stepProgress,
            Action<string> setStatusText,
            ISplashLogoPresenter splashLogo)
        {
            this.sceneLoader = sceneLoader;
            this.stepProgress = stepProgress;
            this.setStatusText = setStatusText;
            this.splashLogo = splashLogo;
        }

        public ISceneLoader SceneLoader
        {
            get { return sceneLoader; }
        }

        public IProgress<float> StepProgress
        {
            get { return stepProgress; }
        }

        public Action<string> SetStatusText
        {
            get { return setStatusText; }
        }

        public ISplashLogoPresenter SplashLogo
        {
            get { return splashLogo; }
        }

        public SceneHandle PersistentSceneHandle { get; set; }
        public SceneHandle GameplaySceneHandle { get; set; }

        public void SetStepProgress(IProgress<float> progress)
        {
            stepProgress = progress;
        }
    }
}
