using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkAIAction
    {
        [ProtoMember(1)]
        public string Id { get; set; }

        [ProtoMember(2)]
        public string Name { get; set; }

        [ProtoMember(3)]
        public string UnitId { get; set; }

        [ProtoMember(4)]
        public string TargetId { get; set; }

        [ProtoMember(5)]
        public NetworkAIDecisionContext DecisionContext { get; set; }

        [ProtoMember(6)]
        public string ActionType { get; set; }

        [ProtoMember(7)]
        public bool UseCommand { get; set; }
    }
}
