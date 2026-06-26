namespace Game.Runtime.Bootstrap
{
    using System;
    using Game.Runtime.Bootstrap.UI;
    using Game.Runtime.Scenes;

    public interface IBootContext
    {
        ISceneLoader SceneLoader { get; }
        IProgress<float> StepProgress { get; }
        Action<string> SetStatusText { get; }

        /// <summary>
        /// Splash-screen logo overlay. Null if the boot scene was set up
        /// without a splash view (headless tests). Steps that target the
        /// logo must guard for null before driving it.
        /// </summary>
        ISplashLogoPresenter SplashLogo { get; }

        SceneHandle PersistentSceneHandle { get; set; }
        SceneHandle GameplaySceneHandle { get; set; }
    }
}
