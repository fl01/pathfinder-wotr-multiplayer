using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkDiscrepantDLC
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkDLC DLC { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public string Reason { get; set; }
    }
}
