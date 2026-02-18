using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkGlobalMapCommonPopup
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkGlobalMapLocation Location { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public string Type { get; set; }
    }
}
