using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkTrapDisarm
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkMapObject MapObject { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public int Roll { get; set; }

        [ProtoMember(3)]
        [LogMe]
        public bool IsSuccess { get; set; }

        [ProtoMember(4)]
        [LogMe]
        public string UnitId { get; set; }
    }
}
