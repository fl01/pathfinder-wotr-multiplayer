using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkAIAction
    {
        [ProtoMember(1)]
        [LogMe]
        public string Id { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public string Name { get; set; }

        [ProtoMember(3)]
        [LogMe]
        public string UnitId { get; set; }

        [ProtoMember(4)]
        [LogMe]
        public string TargetId { get; set; }

        [ProtoMember(5)]
        [LogMe]
        public NetworkAIDecisionContext DecisionContext { get; set; }

        [ProtoMember(6)]
        [LogMe]
        public bool IsAbility { get; set; }
    }
}
