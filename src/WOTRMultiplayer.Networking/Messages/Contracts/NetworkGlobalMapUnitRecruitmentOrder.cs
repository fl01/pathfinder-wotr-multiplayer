using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkGlobalMapUnitRecruitmentOrder
    {
        [ProtoMember(1)]
        [LogMe]
        public string ArmyId { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public string BlueprintId { get; set; }

        [ProtoMember(3)]
        [LogMe]
        public int Count { get; set; }

        [ProtoMember(4)]
        [LogMe]
        public string Type { get; set; }
    }
}
