using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkGlobalMapArmy
    {
        [ProtoMember(1)]
        [LogMe]
        public string Id { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public string Name { get; set; }
    }
}
