using WOTRMultiplayer.Abstractions.Settings;
using WOTRMultiplayer.MP.Entities.Settings;

namespace WOTRMultiplayer.Settings
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
            _settingsControllerAccessor.CreateDefaultValue(WellKnownSettings.General.PlayerName);
            _settingsControllerAccessor.CreateDefaultValue(WellKnownSettings.Combat.SyncAI);
            _settingsControllerAccessor.CreateDefaultValue(WellKnownSettings.Networking.HostPortRangeStart);
            _settingsControllerAccessor.CreateDefaultValue(WellKnownSettings.Networking.HostPortRangeEnd);
            _settingsControllerAccessor.CreateDefaultValue<string>(WellKnownSettings.DangerZone.DefaultForcedPauseTimeout);
            _settingsControllerAccessor.CreateDefaultValue<string>(WellKnownSettings.DangerZone.RestEncounterForcedPauseTimeout);
        }

        public NetworkMultiplayerSettings GetSettings()
        {
            var settings = new NetworkMultiplayerSettings
            {
                PlayerName = _settingsControllerAccessor.GetValue(WellKnownSettings.General.PlayerName),
                SyncAICombatActions = _settingsControllerAccessor.GetValue(WellKnownSettings.Combat.SyncAI),
                HostPortRangeStart = _settingsControllerAccessor.GetValue(WellKnownSettings.Networking.HostPortRangeStart),
                HostPortRangeEnd = _settingsControllerAccessor.GetValue(WellKnownSettings.Networking.HostPortRangeEnd),
                ForcedPauseDefaultTerminationDelay = _settingsControllerAccessor.GetTimeSpanValue(WellKnownSettings.DangerZone.DefaultForcedPauseTimeout),
                ForcedPauseRandomEncounterTerminationDelay = _settingsControllerAccessor.GetTimeSpanValue(WellKnownSettings.DangerZone.RestEncounterForcedPauseTimeout),
            };

            return settings;
        }
    }
}
