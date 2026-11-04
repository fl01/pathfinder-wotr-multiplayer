using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkCraftItem
    {
        [ProtoMember(1)]
        [LogMe]
        public string BlueprintId { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public int? MetamagicMask { get; set; }
    }
}
