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

        public static implicit operator WellKnownSettingKey<string>(WellKnownSettingKey<T> value)
        {
            var settingAsString = new WellKnownSettingKey<string>(value.DefaultValue?.ToString())
            {
                Key = value.Key
            };

            return settingAsString;
        }
    }
}
