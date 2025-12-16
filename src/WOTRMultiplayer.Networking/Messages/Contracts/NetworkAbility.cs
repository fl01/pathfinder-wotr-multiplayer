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
        public string TargetId { get; set; }

        [ProtoMember(5)]
        public NetworkVector3 TargetPoint { get; set; }

        [ProtoMember(6)]
        public List<NetworkVector3> VectorPath { get; set; }

        [ProtoMember(7)]
        public string CommandType { get; set; }

        [ProtoMember(8)]
        public string Name { get; set; }

        [ProtoMember(9)]
        public string ConvertedFromId { get; set; }
    }
}
