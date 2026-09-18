using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkUnitPartTacticalCombat
    {
        [ProtoMember(1)]
        [LogMe]
        public string SquadId { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public int Count { get; set; }
    }
}
