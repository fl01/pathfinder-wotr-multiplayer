using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyKingdomSettlementEntered)]
    public class NotifyKingdomSettlementEntered
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkKingdomSettlement Settlement { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public bool RequiresUnloadEvent { get; set; }

        [ProtoMember(3)]
        [LogMe]
        public bool ExitSettlementToGlobalMap { get; set; }
    }
}
