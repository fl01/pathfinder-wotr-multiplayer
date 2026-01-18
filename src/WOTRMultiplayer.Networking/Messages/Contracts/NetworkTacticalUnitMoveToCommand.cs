using System.Collections.Generic;
using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkTacticalUnitMoveToCommand
    {
        [ProtoMember(1)]
        public string UnitId { get; set; }

        [ProtoMember(2)]
        public List<NetworkVector3> Path { get; set; }
    }
}
