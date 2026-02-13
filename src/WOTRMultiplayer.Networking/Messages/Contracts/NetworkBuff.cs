using System;
using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkBuff
    {
        [ProtoMember(1)]
        public string Id { get; set; }

        [ProtoMember(2)]
        public string BlueprintId { get; set; }

        [ProtoMember(3)]
        public string Name { get; set; }

        [ProtoMember(4)]
        public bool IsPermanent { get; set; }

        [ProtoMember(5)]
        public TimeSpan TimeLeft { get; set; }

        [ProtoMember(6)]
        public TimeSpan NextTickTime { get; set; }

        [ProtoMember(7)]
        public TimeSpan NextResourceSpendingTime { get; set; }

        [ProtoMember(8)]
        public string CasterId { get; set; }

        [ProtoMember(9)]
        public int Rank { get; set; }

        [ProtoMember(10)]
        public string SourceAbilityCasterId { get; set; }

        [ProtoMember(11)]
        public NetworkAbility SourceAbility { get; set; }

        [ProtoMember(12)]
        public NetworkAbilityParams SourceAbilityParams { get; set; }

        [ProtoMember(13)]
        public bool IsHidden { get; set; }
    }
}
