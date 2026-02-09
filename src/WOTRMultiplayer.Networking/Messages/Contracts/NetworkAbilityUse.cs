using System.Collections.Generic;
using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkAbilityUse
    {
        [ProtoMember(1)]
        public NetworkAbility Ability { get; set; }

        [ProtoMember(2)]
        public string InitiatorUnitId { get; set; }

        [ProtoMember(3)]
        public NetworkTargetWrapper Target { get; set; }

        [ProtoMember(4)]
        public List<NetworkVector3> VectorPath { get; set; }

        [ProtoMember(5)]
        public string CommandType { get; set; }

        [ProtoMember(6)]
        public string MovementLimit { get; set; }
    }
}
