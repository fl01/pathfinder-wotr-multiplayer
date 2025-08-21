using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkCharacterMove
    {
        [ProtoMember(1)]
        public string UnitId { get; set; }

        [ProtoMember(2)]
        public NetworkVector3 Destination { get; set; }

        [ProtoMember(3)]
        public float Delay { get; set; }

        [ProtoMember(4)]
        public float Orientation { get; set; }
    }
}
