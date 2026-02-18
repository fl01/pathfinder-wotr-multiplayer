using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyInventoryItemUsed)]
    public class NotifyInventoryItemUsed
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkUseInventoryItem UseItem { get; set; }
    }
}
