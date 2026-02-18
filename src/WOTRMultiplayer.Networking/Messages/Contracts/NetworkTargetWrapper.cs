using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkTargetWrapper
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkVector3 Point { get; set; }

        [ProtoMember(2)]
        public float? Orientation { get; set; }

        [ProtoMember(3)]
        [LogMe]
        public string UnitId { get; set; }
    }
}
