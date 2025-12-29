using System;
using WOTRMultiplayer.Services.Settings;

namespace WOTRMultiplayer.Abstractions.Settings
{
    public interface ISettingsControllerAccessor
    {
        T GetValue<T>(WellKnownSettingKey<T> key);

        TimeSpan GetTimeSpanValue(WellKnownSettingKey<TimeSpan> key);

        void CreateDefaultValue<TValue>(WellKnownSettingKey<TValue> settingKey);
    }
}
