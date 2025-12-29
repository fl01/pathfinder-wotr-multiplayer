using System;
using System.Collections.Generic;
using System.Reflection;
using WOTRMultiplayer.Abstractions.Settings;
using WOTRMultiplayer.Services.Settings;

namespace WOTRMultiplayer.Playground.Core.Dummies
{
    public class DummySettingsControllerAccessor : ISettingsControllerAccessor
    {
        private readonly Dictionary<string, string> _defaultStringKeys = new()
        {
            { WellKnownSettings.General.PlayerName.Key, Assembly.GetEntryAssembly().GetName().Name }
        };

        public void CreateDefaultValue<TValue>(WellKnownSettingKey<TValue> settingKey)
        {
        }

        public TimeSpan GetTimeSpanValue(WellKnownSettingKey<TimeSpan> key)
        {
            return key.DefaultValue;
        }

        public T GetValue<T>(WellKnownSettingKey<T> key)
        {
            if (!_defaultStringKeys.TryGetValue(key.Key, out var value))
            {
                return key.DefaultValue;
            }

            return (T)(object)value;
        }
    }
}
