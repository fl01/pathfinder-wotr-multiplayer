using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkPolymorphicItem
    {
        [ProtoMember(1)]
        [LogMe]
        public string UnitId { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public NetworkEquipmentSlotPosition Position { get; set; }

        [ProtoMember(3)]
        [LogMe]
        public NetworkItem Item { get; set; }
    }
}
