using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkUseInventoryItem
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkItem Item { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public string UserUnitId { get; set; }

        [ProtoMember(3)]
        [LogMe]
        public NetworkTargetWrapper Target { get; set; }

        [ProtoMember(4)]
        [LogMe]
        public NetworkEquipmentSlotPosition SlotPosition { get; set; }
    }
}
