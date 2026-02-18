using System;
using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkUnitProneState
    {
        [ProtoMember(1)]
        [LogMe]
        public bool Active { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public TimeSpan Duration { get; set; }
    }
}
