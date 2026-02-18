using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkLevelingFeature
    {
        [ProtoMember(1)]
        [LogMe]
        public string Name { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public string Id { get; set; }
    }
}
