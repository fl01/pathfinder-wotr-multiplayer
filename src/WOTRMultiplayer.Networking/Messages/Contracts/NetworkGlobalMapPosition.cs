using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkGlobalMapPosition
    {
        [ProtoMember(1)]
        [LogMe]
        public float EdgePosition { get; set; }
    }
}
