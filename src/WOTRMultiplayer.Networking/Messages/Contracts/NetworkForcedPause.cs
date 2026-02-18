using System;
using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkForcedPause
    {
        [ProtoMember(1)]
        [LogMe]
        public string Reason { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public TimeSpan? RemovalDelay { get; set; }
    }
}
