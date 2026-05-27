#nullable enable

using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

namespace Game.Infrastructure.Persistence
{
    [DisallowMultipleComponent]
    public sealed class AutosaveDriver : MonoBehaviour
    {
        [Inject] private IPlayerSnapshotService? snapshotService;
        [Inject] private AutosaveScheduler? scheduler;
        [Inject] private PersistenceSettings? settings;

        private Action<Exception>? logException;

        private void Awake()
        {
            this.logException = ex =>
            {
                if (ex is OperationCanceledException) return;
                Debug.LogException(ex, this);
            };
        }

        private void Update()
        {
            if (settings == null || snapshotService == null || scheduler == null)
            {
                return;
            }

            if (!settings.AutoSave || !snapshotService.IsReady)
            {
                return;
            }

            if (!scheduler.ShouldSave(Time.unscaledTime))
            {
                return;
            }

            snapshotService.SaveNowAsync().AsUniTask().Forget(this.logException);
        }
    }
}
