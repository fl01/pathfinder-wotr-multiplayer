using System;

namespace WOTRMultiplayer.Services.Settings
{
    public class WellKnownSettingKey<T>
    {
        public string Key { get; set; }

        public T DefaultValue { get; private set; }

        public WellKnownSettingKey(T defaultValue)
        {
            DefaultValue = defaultValue;
        }

        public WellKnownSettingKey<string> AsStringSetting()
        {
            var defaultValue = DefaultValue is TimeSpan ts ? ts.ToString(@"hh\:mm\:ss\.fff") : DefaultValue?.ToString();
            return new WellKnownSettingKey<string>(defaultValue)
            {
                Key = Key
            };
        }
    }
}
