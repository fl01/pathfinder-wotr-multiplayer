using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkEquipmentSlot
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkEquipmentSlotPosition Position { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public NetworkItem Item { get; set; }

        [ProtoMember(3)]
        [LogMe]
        public string OwnerId { get; set; }

        [ProtoMember(4)]
        [LogMe]
        public NetworkEquipmentSwapContext SwapContext { get; set; }
    }
}
