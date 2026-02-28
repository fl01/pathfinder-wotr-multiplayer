using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkMod
    {
        [ProtoMember(1)]
        [LogMe]
        public string Id { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public string Version { get; set; }

        [ProtoMember(3)]
        [LogMe]
        public bool IsEnabled { get; set; }

        [ProtoMember(4)]
        [LogMe]
        public string Type { get; set; }

        public override string ToString()
        {
            return Id;
        }
    }
}
