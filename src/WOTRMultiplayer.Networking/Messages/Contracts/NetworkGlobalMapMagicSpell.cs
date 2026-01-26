using System.Collections.Generic;
using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkGlobalMapMagicSpell
    {
        [ProtoMember(1)]
        public string Id { get; set; }

        [ProtoMember(2)]
        public string Name { get; set; }

        [ProtoMember(3)]
        public List<string> TargetArmies { get; set; } = [];

        [ProtoMember(4)]
        public NetworkGlobalMapLocation Location { get; set; }
    }
}
