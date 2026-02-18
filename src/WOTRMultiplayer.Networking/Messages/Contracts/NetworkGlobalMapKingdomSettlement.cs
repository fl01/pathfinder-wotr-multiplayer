using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkGlobalMapKingdomSettlement
    {
        [ProtoMember(1)]
        [LogMe]
        public string Id { get; set; }
    }
}
