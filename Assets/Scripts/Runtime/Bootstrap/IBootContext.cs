namespace Game.Runtime.Bootstrap
{
    using System;
    using Game.Runtime.Scenes;

    public interface IBootContext
    {
        ISceneLoader SceneLoader { get; }
        IProgress<float> StepProgress { get; }
        Action<string> SetStatusText { get; }

        SceneHandle PersistentSceneHandle { get; set; }
        SceneHandle GameplaySceneHandle { get; set; }
        SceneHandle BootSceneHandle { get; set; }
    }
}
