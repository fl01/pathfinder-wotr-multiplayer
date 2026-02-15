using System.Collections.Generic;
using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkOvertip
    {
        [ProtoMember(1)]
        public NetworkMapObject MapObject { get; set; }

        [ProtoMember(2)]
        public List<string> Units { get; set; } = [];

        [ProtoMember(3)]
        public List<NetworkVector3> VectorPath { get; set; } = [];
    }
}
