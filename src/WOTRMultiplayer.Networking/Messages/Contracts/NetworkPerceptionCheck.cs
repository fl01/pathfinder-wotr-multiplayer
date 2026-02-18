using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkPerceptionCheck
    {
        [ProtoMember(1)]
        [LogMe]
        public string UnitId { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public NetworkMapObject MapObject { get; set; }

        [ProtoMember(3)]
        [LogMe]
        public int Roll { get; set; }
    }
}
