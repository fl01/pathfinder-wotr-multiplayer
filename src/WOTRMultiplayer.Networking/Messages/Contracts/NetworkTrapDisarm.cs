using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkTrapDisarm
    {
        [ProtoMember(1)]
        public NetworkMapObject MapObject { get; set; }

        [ProtoMember(2)]
        public int Roll { get; set; }

        [ProtoMember(3)]
        public bool IsSuccess { get; set; }

        [ProtoMember(4)]
        public string UnitId { get; set; }
    }
}
