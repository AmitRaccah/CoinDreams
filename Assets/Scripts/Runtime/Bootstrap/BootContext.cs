namespace Game.Runtime.Bootstrap
{
    using System;
    using Game.Runtime.Scenes;

    public sealed class BootContext : IBootContext
    {
        private readonly ISceneLoader sceneLoader;
        private readonly Action<string> setStatusText;
        private IProgress<float> stepProgress;

        public BootContext(
            ISceneLoader sceneLoader,
            IProgress<float> stepProgress,
            Action<string> setStatusText)
        {
            this.sceneLoader = sceneLoader;
            this.stepProgress = stepProgress;
            this.setStatusText = setStatusText;
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

        public SceneHandle PersistentSceneHandle { get; set; }
        public SceneHandle GameplaySceneHandle { get; set; }

        public void SetStepProgress(IProgress<float> progress)
        {
            stepProgress = progress;
        }
    }
}
