using System.Collections.Generic;
using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkAbility
    {
        [ProtoMember(1)]
        public string Id { get; set; }

        [ProtoMember(2)]
        public string SpellbookId { get; set; }

        [ProtoMember(3)]
        public string CasterId { get; set; }

        [ProtoMember(4)]
        public NetworkTargetWrapper Target { get; set; }

        [ProtoMember(5)]
        public List<NetworkVector3> VectorPath { get; set; }

        [ProtoMember(6)]
        public string CommandType { get; set; }

        [ProtoMember(7)]
        public string Name { get; set; }

        [ProtoMember(8)]
        public string ConvertedFromId { get; set; }

        [ProtoMember(9)]
        public string MovementLimit { get; set; }
    }
}
