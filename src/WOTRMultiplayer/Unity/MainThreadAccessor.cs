using System;
using Microsoft.Extensions.Logging;
using UniRx;
using WOTRMultiplayer.Abstractions.Unity;

namespace WOTRMultiplayer.Unity
{
    public class MainThreadAccessor : IMainThreadAccessor
    {
        private readonly ILogger<MainThreadAccessor> _logger;

        public MainThreadAccessor(ILogger<MainThreadAccessor> logger)
        {
            _logger = logger;
        }

        public void Enqueue(Action action)
        {
            MainThreadDispatcher.Post(x => action(), null);
        }
    }
}
