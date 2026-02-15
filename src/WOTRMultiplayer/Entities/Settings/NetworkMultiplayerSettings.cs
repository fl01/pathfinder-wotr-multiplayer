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

        public TimeSpan RestEncounterSyncTimeout { get; set; }

        public bool HideServerAddress { get; set; }

        public bool TrackConnectionHistory { get; set; }

        public int MaxConnectionHistoryRecords { get; set; }

        public float DialogSelectedAnswerAnimationDuration { get; set; }

        public float DialogNonSelectedAnswerAnimationDuration { get; set; }

        public float DialogBlockedAnswerAnimationDuration { get; set; }

        public float EnforcedCombatStartDelay { get; internal set; }
    }
}
