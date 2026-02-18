using System;
using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkMultiplayerSettings
    {
        [ProtoMember(1)]
        [LogMe]
        public TimeSpan RemoteRollRetrievalTimeout { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public TimeSpan NetworkAwaiterTimeout { get; set; }

        [ProtoMember(3)]
        [LogMe]
        public TimeSpan RestEncounterSyncTimeout { get; set; }

        [ProtoMember(4)]
        [LogMe]
        public TimeSpan CombatTurnDelayForAI { get; set; }
    }
}
