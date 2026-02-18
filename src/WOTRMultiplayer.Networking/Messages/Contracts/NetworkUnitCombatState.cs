using System.Collections.Generic;
using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkUnitCombatState
    {
        [ProtoMember(1)]
        [LogMe]
        public List<string> EngagedUnits { get; set; } = [];

        [ProtoMember(2)]
        [LogMe]
        public List<string> EngagedBy { get; set; } = [];

        [ProtoMember(3)]
        [LogMe]
        public bool NotSurprised { get; set; }
    }
}
