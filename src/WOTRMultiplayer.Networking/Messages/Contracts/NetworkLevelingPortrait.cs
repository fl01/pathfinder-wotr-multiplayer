using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkLevelingPortrait
    {
        [ProtoMember(1)]
        [LogMe]
        public string Name { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public string CustomId { get; set; }

        [ProtoMember(3)]
        [LogMe]
        public string Category { get; set; }
    }
}
