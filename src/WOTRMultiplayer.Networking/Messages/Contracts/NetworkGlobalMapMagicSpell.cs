using System.Collections.Generic;
using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkGlobalMapMagicSpell
    {
        [ProtoMember(1)]
        [LogMe]
        public string Id { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public string Name { get; set; }

        [ProtoMember(3)]
        [LogMe]
        public List<string> TargetArmies { get; set; } = [];

        [ProtoMember(4)]
        [LogMe]
        public NetworkGlobalMapLocation Location { get; set; }
    }
}
