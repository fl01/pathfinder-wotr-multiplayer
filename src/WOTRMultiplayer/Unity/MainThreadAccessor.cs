using System;
using System.Collections.Concurrent;
using WOTRMultiplayer.Abstractions.Unity;

namespace WOTRMultiplayer.Unity
{
    public class MainThreadAccessor : IMainThreadAccessor
    {
        public ConcurrentQueue<Action> MainThreadQueue { get; private set; }

        public void SetQueue(ConcurrentQueue<Action> mainThreadQueue)
        {
            MainThreadQueue = mainThreadQueue;
        }
    }
}
