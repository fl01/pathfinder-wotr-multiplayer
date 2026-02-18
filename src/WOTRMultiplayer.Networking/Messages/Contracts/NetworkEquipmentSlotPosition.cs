using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkEquipmentSlotPosition
    {
        [ProtoMember(1)]
        [LogMe]
        public string Type { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public int Index { get; set; }
    }
}
