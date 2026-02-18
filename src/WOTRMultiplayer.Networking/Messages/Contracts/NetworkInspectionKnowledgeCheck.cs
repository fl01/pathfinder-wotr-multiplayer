using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkInspectionKnowledgeCheck
    {
        [ProtoMember(1)]
        [LogMe]
        public string TargetUnitId { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public string InitiatorUnitId { get; set; }

        [ProtoMember(3)]
        [LogMe]
        public string StatType { get; set; }

        [ProtoMember(4)]
        [LogMe]
        public int DC { get; set; }

        [ProtoMember(5)]
        [LogMe]
        public string InspectionBlueprintId { get; set; }

        [ProtoMember(6)]
        [LogMe]
        public int RollResult { get; set; }
    }
}
