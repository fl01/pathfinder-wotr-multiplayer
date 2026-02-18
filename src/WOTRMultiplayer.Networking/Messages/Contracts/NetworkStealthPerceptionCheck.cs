using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkStealthPerceptionCheck
    {
        [ProtoMember(1)]
        [LogMe]
        public string InitiatorId { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public string StealthedUnitId { get; set; }

        [ProtoMember(3)]
        [LogMe]
        public int Roll { get; set; }

        [ProtoMember(4)]
        [LogMe]
        public bool IsSuccess { get; set; }

        [ProtoMember(5)]
        [LogMe]
        public int DC { get; set; }

        [ProtoMember(6)]
        [LogMe]
        public bool IsTargetInvisible { get; set; }

        [ProtoMember(7)]
        [LogMe]
        public bool IgnoreDifficultyBonusToDC { get; set; }
    }
}
