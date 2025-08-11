using System.Collections.Generic;
using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkRollValue
    {
        [ProtoMember(1)]
        public int Result { get; set; }

        [ProtoMember(2)]
        public List<int> RollHistory { get; set; } = [];

        [ProtoMember(3)]
        public List<NetworkDamageRollValue> DamageValues { get; set; } = [];

        [ProtoMember(4)]
        public Dictionary<string, int> NamedIntValues { get; set; } = [];
    }
}
