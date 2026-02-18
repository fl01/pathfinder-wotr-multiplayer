using System.Collections.Generic;
using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkRollValue
    {
        [ProtoMember(1)]
        [LogMe]
        public int Result { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public List<int> RollHistory { get; set; } = [];

        [ProtoMember(3)]
        [LogMe]
        public List<NetworkDamageRollValue> DamageValues { get; set; } = [];

        [ProtoMember(4)]
        [LogMe]
        public Dictionary<string, int> NamedIntValues { get; set; } = [];
    }
}
