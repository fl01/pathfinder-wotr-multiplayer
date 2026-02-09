using System.Collections.Generic;
using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkUnit
    {
        [ProtoMember(1)]
        public string Id { get; set; }

        [ProtoMember(2)]
        public NetworkVector3 Position { get; set; }

        [ProtoMember(3)]
        public float Orientation { get; set; }

        [ProtoMember(4)]
        public NetworkUnitTurnBasedInfo TurnBasedInfo { get; set; }

        [ProtoMember(5)]
        public NetworkUnitCombatState CombatState { get; set; }

        [ProtoMember(6)]
        public NetworkUnitDescriptor Descriptor { get; set; }

        [ProtoMember(7)]
        public List<NetworkBuff> Buffs { get; set; } = [];
    }
}
