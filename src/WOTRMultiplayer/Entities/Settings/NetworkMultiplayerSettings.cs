using System;

namespace WOTRMultiplayer.Entities.Settings
{
    public class NetworkMultiplayerSettings
    {
        public string PlayerName { get; set; }

        public int HostPortRangeStart { get; set; }

        public int HostPortRangeEnd { get; set; }

        public TimeSpan ForcedPauseDefaultTerminationDelay { get; set; }

        public TimeSpan ForcedPauseRandomEncounterTerminationDelay { get; set; }

        public TimeSpan RemoteRollRetrievalTimeout { get; set; }

        public TimeSpan NetworkAwaiterTimeout { get; set; }

        public TimeSpan AISyncTimeout { get; set; }

        public bool SyncAICombatActions { get; set; }

        public TimeSpan RestEncounterSyncTimeout { get; set; }
    }
}
