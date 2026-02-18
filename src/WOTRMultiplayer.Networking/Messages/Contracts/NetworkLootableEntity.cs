using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkLootableEntity
    {
        [ProtoMember(1)]
        [LogMe]
        public string Id { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public NetworkVector3 Position { get; set; }

        [ProtoMember(3)]
        [LogMe]
        public string Type { get; set; }
    }
}
