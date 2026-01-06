using WOTRMultiplayer.Abstractions.Settings;
using WOTRMultiplayer.Entities.Settings;

namespace WOTRMultiplayer.Services.Settings
{
    public class MultiplayerSettingsProvider : IMultiplayerSettingsService
    {
        private readonly ISettingsControllerAccessor _settingsControllerAccessor;

        public MultiplayerSettingsProvider(ISettingsControllerAccessor settingsControllerAccessor)
        {
            _settingsControllerAccessor = settingsControllerAccessor;
        }

        public void Initialize()
        {
            // general
            _settingsControllerAccessor.CreateDefaultValue(WellKnownSettings.General.PlayerName);

            // combat
            _settingsControllerAccessor.CreateDefaultValue(WellKnownSettings.Combat.AISync);

            // networking
            _settingsControllerAccessor.CreateDefaultValue(WellKnownSettings.Networking.HostPortRangeStart);
            _settingsControllerAccessor.CreateDefaultValue(WellKnownSettings.Networking.HostPortRangeEnd);

            // hotkeys
            _settingsControllerAccessor.CreateDefaultValue(WellKnownSettings.Hotkeys.Ping);

            // misc
            _settingsControllerAccessor.CreateDefaultValue(WellKnownSettings.Miscellaneous.HideServerAddress);

            // danger zone
            _settingsControllerAccessor.CreateDefaultValue<string>(WellKnownSettings.DangerZone.DefaultForcedPauseTimeout);
            _settingsControllerAccessor.CreateDefaultValue<string>(WellKnownSettings.DangerZone.RestEncounterForcedPauseTimeout);
            _settingsControllerAccessor.CreateDefaultValue<string>(WellKnownSettings.DangerZone.RemoteRollRetrievalTimeout);
            _settingsControllerAccessor.CreateDefaultValue<string>(WellKnownSettings.DangerZone.NetworkAwaiterTimeout);
            _settingsControllerAccessor.CreateDefaultValue<string>(WellKnownSettings.DangerZone.AISyncTimeout);
            _settingsControllerAccessor.CreateDefaultValue<string>(WellKnownSettings.DangerZone.RestEncounterSyncTimeout);

        }

        public NetworkMultiplayerSettings GetSettings()
        {
            var settings = new NetworkMultiplayerSettings
            {
                PlayerName = _settingsControllerAccessor.GetValue(WellKnownSettings.General.PlayerName),

                SyncAICombatActions = _settingsControllerAccessor.GetValue(WellKnownSettings.Combat.AISync),

                HostPortRangeStart = _settingsControllerAccessor.GetValue(WellKnownSettings.Networking.HostPortRangeStart),
                HostPortRangeEnd = _settingsControllerAccessor.GetValue(WellKnownSettings.Networking.HostPortRangeEnd),

                HideServerAddress = _settingsControllerAccessor.GetValue(WellKnownSettings.Miscellaneous.HideServerAddress),

                ForcedPauseDefaultTerminationDelay = _settingsControllerAccessor.GetTimeSpanValue(WellKnownSettings.DangerZone.DefaultForcedPauseTimeout),
                ForcedPauseRandomEncounterTerminationDelay = _settingsControllerAccessor.GetTimeSpanValue(WellKnownSettings.DangerZone.RestEncounterForcedPauseTimeout),
                RemoteRollRetrievalTimeout = _settingsControllerAccessor.GetTimeSpanValue(WellKnownSettings.DangerZone.RemoteRollRetrievalTimeout),
                NetworkAwaiterTimeout = _settingsControllerAccessor.GetTimeSpanValue(WellKnownSettings.DangerZone.NetworkAwaiterTimeout),
                AISyncTimeout = _settingsControllerAccessor.GetTimeSpanValue(WellKnownSettings.DangerZone.AISyncTimeout),
                RestEncounterSyncTimeout = _settingsControllerAccessor.GetTimeSpanValue(WellKnownSettings.DangerZone.RestEncounterSyncTimeout),
            };

            return settings;
        }
    }
}
