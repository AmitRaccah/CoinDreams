using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Game.Runtime.Cards
{
    public abstract class InterruptibleAsyncOperationBehaviour : MonoBehaviour
    {
        private Coroutine activeOperationCoroutine;
        private TaskCompletionSource<bool> activeOperationCompletionSource;
        private CancellationTokenSource cancellationSource;

        protected bool HasActiveOperation
        {
            get { return activeOperationCoroutine != null; }
        }

        public CancellationToken Token
        {
            get { return cancellationSource?.Token ?? new CancellationToken(true); }
        }

        protected Task RunOperationAsync(Func<IEnumerator> coroutineFactory)
        {
            if (coroutineFactory == null)
            {
                return Task.FromException(
                    new ArgumentNullException(nameof(coroutineFactory)));
            }

            if (activeOperationCoroutine != null)
            {
                return activeOperationCompletionSource != null
                    ? activeOperationCompletionSource.Task
                    : Task.CompletedTask;
            }

            IEnumerator coroutine;
            try
            {
                coroutine = coroutineFactory();
            }
            catch (Exception exception)
            {
                return Task.FromException(exception);
            }

            if (coroutine == null)
            {
                return Task.CompletedTask;
            }

            cancellationSource = new CancellationTokenSource();
            activeOperationCompletionSource = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            activeOperationCoroutine = StartCoroutine(coroutine);
            return activeOperationCompletionSource.Task;
        }

        protected void CompleteOperation()
        {
            activeOperationCoroutine = null;

            TaskCompletionSource<bool> completionSource = activeOperationCompletionSource;
            activeOperationCompletionSource = null;

            CancellationTokenSource source = cancellationSource;
            cancellationSource = null;
            source?.Dispose();

            completionSource?.TrySetResult(true);
        }

        private void OnDisable()
        {
            CancelOperation(GetDisableCancellationMessage());
        }

        private void OnDestroy()
        {
            CancelOperation(GetDestroyCancellationMessage());
        }

        private void CancelOperation(string message)
        {
            if (activeOperationCoroutine != null)
            {
                StopCoroutine(activeOperationCoroutine);
                activeOperationCoroutine = null;
            }

            CancellationTokenSource source = cancellationSource;
            cancellationSource = null;
            CancellationToken cancellationToken = source?.Token ?? CancellationToken.None;
            try
            {
                source?.Cancel();
            }
            finally
            {
                source?.Dispose();
            }

            TaskCompletionSource<bool> completionSource = activeOperationCompletionSource;
            activeOperationCompletionSource = null;
            if (completionSource == null)
            {
                return;
            }

            // Use TrySetCanceled so awaiters observe a TaskCanceledException tied to the token,
            // matching standard cancellation semantics rather than a generic exception.
            _ = message;
            completionSource.TrySetCanceled(cancellationToken);
        }

        protected abstract string GetDisableCancellationMessage();
        protected abstract string GetDestroyCancellationMessage();
    }
}
