using System;
using System.Linq;
using FakeItEasy;
using WOTRMultiplayer.Networking.Abstractions;

namespace WOTRMultiplayer.UnitTests
{
    public static class FakeUtils
    {
        public static Action<long, T> GetNetworkReceiverHandler<T>(INetworkReceiver networkReceiverFake)
        {
            var calls = Fake.GetCalls(networkReceiverFake).ToList();
            var setupHandlerCalls = calls.Where(x => x.Method.IsGenericMethod).ToList();
            var targetHandler = setupHandlerCalls.FirstOrDefault(x => x.Method.GetGenericArguments().Any(x => x == typeof(T)))?.Arguments.First() as Action<long, T>;
            return targetHandler;
        }
    }
}
