using System;
using UniRx;
using WOTRMultiplayer.Abstractions.Unity;

namespace WOTRMultiplayer.Unity
{
    public class MainThreadAccessor : IMainThreadAccessor
    {
        public void Post(Action action)
        {
            MainThreadDispatcher.Post(x => action(), null);
        }
    }
}
