using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyInventoryItemTransferred)]
    public class NotifyInventoryItemTransferred : IForwardableMessage
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkItemsTransfer TransferItem { get; set; }
    }
}
