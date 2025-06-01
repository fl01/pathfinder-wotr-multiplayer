using System;
using System.Collections.Concurrent;

namespace WOTRMultiplayer.Abstractions.Unity
{
    public interface IMainThreadAccessor
    {
        void Enqueue(Action action);
    }
}
