using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyEquipmentSlotChanged)]
    public class NotifyEquipmentSlotChanged : IForwardableMessage
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkEquipmentSlot Slot { get; set; }
    }
}
