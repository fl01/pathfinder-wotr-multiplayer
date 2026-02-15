using System;
using System.Threading.Tasks;

namespace WOTRMultiplayer.Abstractions.QueuedActions
{
    public interface IQueuedActionsRunner
    {
        Task RunAsync(Action action, Func<Task> waitForNext);
    }
}
