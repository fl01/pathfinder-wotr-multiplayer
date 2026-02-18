using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkCharacterMove
    {
        [ProtoMember(1)]
        [LogMe]
        public string UnitId { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public NetworkVector3 Destination { get; set; }

        [ProtoMember(3)]
        [LogMe]
        public float Delay { get; set; }

        [ProtoMember(4)]
        [LogMe]
        public float Orientation { get; set; }
    }
}
