using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkUnit
    {
        [ProtoMember(1)]
        public string Id { get; set; }

        [ProtoMember(2)]
        public NetworkVector3 Position { get; set; }

        [ProtoMember(3)]
        public float Orientation { get; set; }

        [ProtoMember(4)]
        public bool? Surprising { get; set; }

        [ProtoMember(5)]
        public bool? Surprised { get; set; }

        [ProtoMember(6)]
        public bool? ActingInSurpriseRound { get; set; }
    }
}
