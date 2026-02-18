using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkEquipmentSwapContext
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkEquipmentSlotPosition From { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public NetworkEquipmentSlotPosition To { get; set; }
    }
}
