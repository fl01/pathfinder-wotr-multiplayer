using ProtoBuf;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyEquipmentSlotChanged)]
    public class NotifyEquipmentSlotChanged
    {
        [ProtoMember(1)]
        public NetworkEquipmentSlot Slot { get; set; }
    }
}
