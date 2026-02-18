using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkDialog
    {
        [ProtoMember(1)]
        [LogMe]
        public string Id { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public string Name { get; set; }

        [ProtoMember(3)]
        [LogMe]
        public string TargetUnitId { get; set; }

        [ProtoMember(4)]
        [LogMe]
        public string InitiatorUnitId { get; set; }

        [ProtoMember(5)]
        [LogMe]
        public string MapObjectId { get; set; }

        [ProtoMember(6)]
        [LogMe]
        public string SpeakerKey { get; set; }

        [ProtoMember(7)]
        [LogMe]
        public bool IsScripted { get; set; }
    }
}
