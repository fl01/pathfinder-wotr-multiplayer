using System;
using System.Collections.Concurrent;

namespace WOTRMultiplayer.Abstractions.Unity
{
    public interface IMainThreadAccessor
    {
        ConcurrentQueue<Action> MainThreadQueue { get; }

        void SetQueue(ConcurrentQueue<Action> mainThreadQueue);
    }
}
