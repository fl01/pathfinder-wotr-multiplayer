using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkAIAction
    {
        [ProtoMember(1)]
        public string UnitId { get; set; }

        [ProtoMember(2)]
        public decimal CurrentScore { get; set; }

        [ProtoMember(3)]
        public string TargetId { get; set; }

        [ProtoMember(4)]
        public string ActionBlueprintId { get; set; }

        [ProtoMember(5)]
        public string ActionType { get; set; }

        [ProtoMember(6)]
        public bool IsAutoUseAbility { get; set; }
    }
}
