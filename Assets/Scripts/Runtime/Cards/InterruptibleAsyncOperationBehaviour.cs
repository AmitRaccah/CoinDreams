using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

namespace Game.Runtime.Cards
{
    public abstract class InterruptibleAsyncOperationBehaviour : MonoBehaviour
    {
        private Coroutine activeOperationCoroutine;
        private TaskCompletionSource<bool> activeOperationCompletionSource;

        protected bool HasActiveOperation
        {
            get { return activeOperationCoroutine != null; }
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

            TaskCompletionSource<bool> completionSource = activeOperationCompletionSource;
            activeOperationCompletionSource = null;
            if (completionSource == null)
            {
                return;
            }

            completionSource.TrySetException(
                new OperationCanceledException(message ?? string.Empty));
        }

        protected abstract string GetDisableCancellationMessage();
        protected abstract string GetDestroyCancellationMessage();
    }
}
