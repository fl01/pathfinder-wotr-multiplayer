using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkVendorItemTransfer
    {
        [ProtoMember(1)]
        public NetworkItem Item { get; set; }

        [ProtoMember(2)]
        public int Count { get; set; }

        [ProtoMember(3)]
        public string ItemAction { get; set; }

        [ProtoMember(4)]
        public string ItemActionTarget { get; set; }
    }
}
