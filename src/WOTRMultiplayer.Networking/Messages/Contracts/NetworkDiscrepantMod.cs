using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkDiscrepantMod
    {
        [ProtoMember(1)]
        [LogMe]
        public string Id { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public string Type { get; set; }

        [ProtoMember(3)]
        public string HostVersion { get; set; }

        [ProtoMember(4)]
        public string Version { get; set; }

        [ProtoMember(5)]
        public string Reason { get; set; }
    }
}
