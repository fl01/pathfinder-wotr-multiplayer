using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkDropItem
    {
        [ProtoMember(1)]
        [LogMe]
        public string OwnerEntityId { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public NetworkItem Item { get; set; }
    }
}
