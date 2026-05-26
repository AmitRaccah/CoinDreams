namespace Game.Runtime.Lifecycle
{
    using System;
    using UnityEngine;

    [DisallowMultipleComponent]
    public sealed class AppLifecycleObserver : MonoBehaviour, IAppLifecycle
    {
        public static AppLifecycleObserver Current { get; private set; }

        public event Action ApplicationPaused;
        public event Action ApplicationResumed;
        public event Action ApplicationQuitting;

        private bool isForeground = true;

        public bool IsForeground
        {
            get { return isForeground; }
        }

        private void Awake()
        {
            if (Current != null && !ReferenceEquals(Current, this))
            {
                Debug.LogWarning(
                    "[AppLifecycleObserver] Multiple AppLifecycleObserver instances found. The newest instance is now Current.",
                    this);
            }

            Current = this;
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(Current, this))
            {
                Current = null;
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                isForeground = false;
                Action handler = ApplicationPaused;
                if (handler != null)
                {
                    handler();
                }
            }
            else
            {
                isForeground = true;
                Action handler = ApplicationResumed;
                if (handler != null)
                {
                    handler();
                }
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
            {
                if (!isForeground)
                {
                    isForeground = true;
                    Action handler = ApplicationResumed;
                    if (handler != null)
                    {
                        handler();
                    }
                }
            }
            else
            {
                if (isForeground)
                {
                    isForeground = false;
                    Action handler = ApplicationPaused;
                    if (handler != null)
                    {
                        handler();
                    }
                }
            }
        }

        private void OnApplicationQuit()
        {
            Action handler = ApplicationQuitting;
            if (handler != null)
            {
                handler();
            }
        }
    }
}
