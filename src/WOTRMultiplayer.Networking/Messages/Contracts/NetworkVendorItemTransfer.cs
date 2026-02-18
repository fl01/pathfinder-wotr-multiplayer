using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkVendorItemTransfer
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkItem Item { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public int Count { get; set; }

        [ProtoMember(3)]
        [LogMe]
        public string ItemAction { get; set; }

        [ProtoMember(4)]
        [LogMe]
        public string ItemActionTarget { get; set; }
    }
}
