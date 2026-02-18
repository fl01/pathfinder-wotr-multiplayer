using System.Collections.Generic;
using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkUnitCombatState
    {
        [ProtoMember(1)]
        public List<string> EngagedUnits { get; set; } = [];

        [ProtoMember(2)]
        public List<string> EngagedBy { get; set; } = [];

        [ProtoMember(3)]
        public bool NotSurprised { get; set; }
    }
}
