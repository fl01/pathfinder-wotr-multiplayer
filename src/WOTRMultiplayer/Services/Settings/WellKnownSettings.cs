using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Kingmaker.Settings;

namespace WOTRMultiplayer.Services.Settings
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

        [Description("dialogs")]
        public static class Dialogs
        {
            [Description("selected-answer-animation-duration")]
            public static WellKnownSettingKey<float> SelectedAnswerAnimationDuration { get; } = new(0.8f);

            [Description("nonselected-answer-animation-duration")]
            public static WellKnownSettingKey<float> NonSelectedAnswerAnimationDuration { get; } = new(0.8f);

            [Description("blocked-answer-animation-duration")]
            public static WellKnownSettingKey<float> BlockedAnswerAnimationDuration { get; } = new(0.8f);
        }

        [Description("networking")]
        public static class Networking
        {
            [Description("host-port-range-start")]
            public static WellKnownSettingKey<int> HostPortRangeStart { get; } = new(1024);

            [Description("host-port-range-end")]
            public static WellKnownSettingKey<int> HostPortRangeEnd { get; } = new(ushort.MaxValue);
        }

        [Description("miscellaneous")]
        public static class Miscellaneous
        {
            [Description("hide-server-address")]
            public static WellKnownSettingKey<bool> HideServerAddress { get; } = new(false);

            [Description("track-connection-history")]
            public static WellKnownSettingKey<bool> TrackConnectionHistory { get; } = new(true);

            [Description("max-connection-history-records")]
            public static WellKnownSettingKey<int> MaxConnectionHistoryRecords { get; } = new(3);
        }

        [Description("hotkeys")]
        public static class Hotkeys
        {
            [Description("ping")]
            public static WellKnownSettingKey<KeyBindingPair> Ping { get; } = new(new KeyBindingPair { GameModesGroup = Kingmaker.UI.KeyboardAccess.GameModesGroup.All, Binding1 = new KeyBindingData { IsShiftDown = true, Key = UnityEngine.KeyCode.T } });

            [Description("force-unpause")]
            public static WellKnownSettingKey<KeyBindingPair> ForceUnpause { get; } = new(new KeyBindingPair { GameModesGroup = Kingmaker.UI.KeyboardAccess.GameModesGroup.All, Binding1 = new KeyBindingData { IsCtrlDown = true, IsShiftDown = true, Key = UnityEngine.KeyCode.F12 } });

            [Description("force-combat-end")]
            public static WellKnownSettingKey<KeyBindingPair> ForceCombatEnd { get; } = new(new KeyBindingPair { GameModesGroup = Kingmaker.UI.KeyboardAccess.GameModesGroup.All, Binding1 = new KeyBindingData { IsCtrlDown = true, IsShiftDown = true, Key = UnityEngine.KeyCode.F9 } });
        }

        [Description("danger-zone")]
        public static class DangerZone
        {
            [Description("default-forced-pause-timeout")]
            public static WellKnownSettingKey<TimeSpan> DefaultForcedPauseTimeout { get; } = new(TimeSpan.FromSeconds(3));

            [Description("rest-encounter-forced-pause-timeout")]
            public static WellKnownSettingKey<TimeSpan> RestEncounterForcedPauseTimeout { get; } = new(TimeSpan.FromSeconds(8));

            [Description("remote-roll-retrieval-timeout")]
            public static WellKnownSettingKey<TimeSpan> RemoteRollRetrievalTimeout { get; } = new(TimeSpan.FromSeconds(5));

            [Description("network-awaiter-timeout")]
            public static WellKnownSettingKey<TimeSpan> NetworkAwaiterTimeout { get; } = new(TimeSpan.FromMinutes(1));

            [Description("rest-encounter-sync-timeout")]
            public static WellKnownSettingKey<TimeSpan> RestEncounterSyncTimeout { get; } = new(TimeSpan.FromSeconds(30));

            [Description("enforced-combat-start-delay")]
            public static WellKnownSettingKey<float> EnforcedCombatStartDelay { get; } = new(0.5f);

            [Description("combat-turn-delay-for-ai")]
            public static WellKnownSettingKey<TimeSpan> CombatTurnDelayForAI { get; } = new(TimeSpan.FromSeconds(0.4d));

            [Description("save-game-chunk-size")]
            public static WellKnownSettingKey<int> SaveGameChunkSize { get; } = new(32768);
        }
    }
}
