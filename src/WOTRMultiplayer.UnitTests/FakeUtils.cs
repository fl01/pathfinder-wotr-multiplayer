using System;
using System.Linq;
using FakeItEasy;
using Kingmaker.Settings;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Networking.Abstractions;
using WOTRMultiplayer.Services.Settings;

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

        public static Action GetHotkeyCallback(WellKnownSettingKey<KeyBindingPair> settingKey, IKeyboardAccessor keyboardAccessorFake)
        {
            var calls = Fake.GetCalls(keyboardAccessorFake).ToList();
            var bindCalls = calls.Where(x => string.Equals(x.Method.Name, nameof(IKeyboardAccessor.Bind))).ToList();
            var bindCall = bindCalls.FirstOrDefault(x => string.Equals(x.Arguments.First() as string, settingKey.Key));
            var callback = bindCall?.Arguments.OfType<Action>().FirstOrDefault();
            return callback;
        }
    }
}
