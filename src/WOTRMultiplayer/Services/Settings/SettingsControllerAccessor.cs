using System;
using Kingmaker.Settings;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Abstractions.Settings;

namespace WOTRMultiplayer.Services.Settings
{
    public class SettingsControllerAccessor : ISettingsControllerAccessor
    {
        private readonly ILogger<SettingsControllerAccessor> _logger;

        public SettingsControllerAccessor(ILogger<SettingsControllerAccessor> logger)
        {
            _logger = logger;
        }

        public T GetValue<T>(WellKnownSettingKey<T> setting)
        {
            var value = GetValue(setting.Key, setting.DefaultValue);
            return value;
        }

        public TimeSpan GetTimeSpanValue(WellKnownSettingKey<TimeSpan> key)
        {
            var value = GetValue<string>(key.Key, null);
            if (string.IsNullOrEmpty(value) || !TimeSpan.TryParse(value, out var timeSpanValue))
            {
                return key.DefaultValue;
            }

            return timeSpanValue;
        }

        public void CreateDefaultValue<TValue>(WellKnownSettingKey<TValue> settingKey)
        {
            if (SettingsController.GeneralSettingsProvider.HasKey(settingKey.Key))
            {
                return;
            }

            SettingsController.GeneralSettingsProvider.SetValue(settingKey.Key, settingKey.DefaultValue);
            _logger.LogInformation("Default value for setting key has been created. Key={Key}, DefaultValue={DefaultValue}", settingKey.Key, settingKey.DefaultValue);
        }

        private T GetValue<T>(string key, T defaultValue)
        {
            try
            {
                var value = SettingsController.GeneralSettingsProvider.GetValue(key, defaultValue);

                return value;
            }
            catch (NullReferenceException ex)
            {
                _logger.LogWarning(ex, "Requested setting doesn't exist. Using default value. Key={Key}, DefaultValue={DefaultValue}", key, defaultValue);
                return defaultValue;
            }
        }
    }
}
