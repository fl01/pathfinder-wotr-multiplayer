using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

namespace WOTRMultiplayer.Settings
{
    // Exception: [Settings] Key can contain only lower letter, numbers, dashes (-) and dots (.)
    [Description(RootKey)]
    public static class WellKnownSettings
    {
        public const string KeyPathSeparator = ".";
        public const string RootKey = "wotrmultiplayer.settings";

        public static void Initialize()
        {
            var rootType = typeof(WellKnownSettings);
            var rootPath = rootType.GetCustomAttribute<DescriptionAttribute>().Description;

            var settingSections = rootType.GetNestedTypes().Where(n => n.GetCustomAttribute<DescriptionAttribute>() != null).ToList();
            foreach (var section in settingSections)
            {
                var sectionName = section.GetCustomAttribute<DescriptionAttribute>().Description;
                var settings = section.GetProperties().Where(n => n.GetCustomAttribute<DescriptionAttribute>() != null).ToList();
                foreach (var setting in settings)
                {
                    var settingName = setting.GetCustomAttribute<DescriptionAttribute>().Description;
                    var key = string.Join(KeyPathSeparator, RootKey, sectionName, settingName);
                    var actualValue = setting.GetValue(null);
                    var keyProperty = actualValue.GetType().GetProperty(nameof(WellKnownSettingKey<string>.Key));
                    keyProperty.SetValue(actualValue, key);
                }
            }
        }

        [Description("general")]
        public static class General
        {
            [Description("player-name")]
            public static WellKnownSettingKey<string> PlayerName { get; } = new(Environment.UserName);
        }

        [Description("combat")]
        public static class Combat
        {
            [Description("ai-sync")]
            public static WellKnownSettingKey<bool> AISync { get; } = new(true);
        }

        [Description("networking")]
        public static class Networking
        {
            [Description("host-port-range-start")]
            public static WellKnownSettingKey<int> HostPortRangeStart { get; } = new(1024);

            [Description("host-port-range-end")]
            public static WellKnownSettingKey<int> HostPortRangeEnd { get; } = new(ushort.MaxValue);
        }

        [Description("danger-zone")]
        public static class DangerZone
        {
            [Description("default-forced-pause-timeout")]
            public static WellKnownSettingKey<TimeSpan> DefaultForcedPauseTimeout { get; } = new(TimeSpan.FromSeconds(3));

            [Description("rest-encounter-forced-pause-timeout")]
            public static WellKnownSettingKey<TimeSpan> RestEncounterForcedPauseTimeout { get; } = new(TimeSpan.FromSeconds(8));

            [Description("remote-roll-retrieval-timeout")]
            public static WellKnownSettingKey<TimeSpan> RemoteRollRetrievalTimeout { get; } = new(TimeSpan.FromSeconds(10));

            [Description("network-awaiter-timeout")]
            public static WellKnownSettingKey<TimeSpan> NetworkAwaiterTimeout { get; } = new(TimeSpan.FromMinutes(1));

            [Description("ai-sync-timeout")]
            public static WellKnownSettingKey<TimeSpan> AISyncTimeout { get; } = new(TimeSpan.FromSeconds(5));

            [Description("rest-encounter-sync-timeout")]
            public static WellKnownSettingKey<TimeSpan> RestEncounterSyncTimeout { get; } = new(TimeSpan.FromSeconds(45));
        }
    }
}
