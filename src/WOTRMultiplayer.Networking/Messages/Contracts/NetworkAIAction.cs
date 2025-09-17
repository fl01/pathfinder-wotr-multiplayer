using System.Collections.Generic;
using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkAIAction
    {
        [ProtoMember(1)]
        public string UnitId { get; set; }

        [ProtoMember(2)]
        public string TargetId { get; set; }

        [ProtoMember(3)]
        public string ActionBlueprintId { get; set; }

        [ProtoMember(4)]
        public string ActionType { get; set; }

        [ProtoMember(5)]
        public bool IsAutoUseAbility { get; set; }

        [ProtoMember(6)]
        public List<NetworkVector3> BestPath { get; set; } = [];

        [ProtoMember(7)]
        public bool BestEnableFiveFootStep { get; set; }
    }
}
