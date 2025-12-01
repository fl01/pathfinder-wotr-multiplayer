using System;
using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkMultiplayerSettings
    {
        [ProtoMember(1)]
        public bool SyncAICombatActions { get; set; }

        [ProtoMember(2)]
        public TimeSpan RemoteRollRetrievalTimeout { get; set; }

        [ProtoMember(3)]
        public TimeSpan NetworkAwaiterTimeout { get; set; }

        [ProtoMember(4)]
        public TimeSpan AISyncTimeout { get; set; }

        [ProtoMember(5)]
        public TimeSpan RestEncounterSyncTimeout { get; set; }
    }
}
