using System;
using System.Threading;
using System.Threading.Tasks;
using WOTRMultiplayer.Abstractions.QueuedActions;

namespace WOTRMultiplayer.Services.QueuedActions
{
    public class QueuedActionsRunner : IQueuedActionsRunner
    {
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        public async Task RunAsync(Action action, Func<Task> waitForNext)
        {
            await _semaphore.WaitAsync();
            try
            {
                await waitForNext();

                action();
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}
