using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkLevelingTattoo
    {
        [ProtoMember(1)]
        [LogMe]
        public int Index { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public int PageNumber { get; set; }

        [ProtoMember(3)]
        [LogMe]
        public string TextureName { get; set; }
    }
}
