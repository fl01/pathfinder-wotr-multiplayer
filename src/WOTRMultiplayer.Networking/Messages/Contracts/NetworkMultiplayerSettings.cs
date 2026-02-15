using System;
using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkMultiplayerSettings
    {
        [ProtoMember(1)]
        public TimeSpan RemoteRollRetrievalTimeout { get; set; }

        [ProtoMember(2)]
        public TimeSpan NetworkAwaiterTimeout { get; set; }

        [ProtoMember(3)]
        public TimeSpan RestEncounterSyncTimeout { get; set; }
    }
}
