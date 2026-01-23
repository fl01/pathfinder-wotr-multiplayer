using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkGlobalMapUnitRecruitmentOrder
    {
        [ProtoMember(1)]
        public string ArmyId { get; set; }

        [ProtoMember(2)]
        public string BlueprintId { get; set; }

        [ProtoMember(3)]
        public int Count { get; set; }

        [ProtoMember(4)]
        public string Type { get; set; }
    }
}
