using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkDialog
    {
        [ProtoMember(1)]
        public string Id { get; set; }

        [ProtoMember(2)]
        public string Name { get; set; }

        [ProtoMember(3)]
        public string TargetUnitId { get; set; }

        [ProtoMember(4)]
        public string InitiatorUnitId { get; set; }

        [ProtoMember(5)]
        public string MapObjectId { get; set; }

        [ProtoMember(6)]
        public string SpeakerKey { get; set; }

        [ProtoMember(7)]
        public bool IsScripted { get; set; }
    }
}
