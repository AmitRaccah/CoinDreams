namespace Game.Runtime.Lifecycle
{
    using System;

    public interface IAppLifecycle
    {
        event Action ApplicationPaused;
        event Action ApplicationResumed;
        event Action ApplicationQuitting;
        bool IsForeground { get; }
    }
}
