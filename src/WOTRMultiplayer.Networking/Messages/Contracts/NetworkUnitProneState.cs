using System;
using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkUnitProneState
    {
        [ProtoMember(1)]
        public bool Active { get; set; }

        [ProtoMember(2)]
        public TimeSpan Duration { get; set; }
    }
}
