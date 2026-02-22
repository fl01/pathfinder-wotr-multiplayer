using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkKingdomSettlementSlot
    {
        [ProtoMember(1)]
        [LogMe]
        public string Id { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public int X { get; set; }

        [ProtoMember(3)]
        [LogMe]
        public int Y { get; set; }

        public override string ToString()
        {
            return $"{Id} <{X},{Y}>";
        }
    }
}
