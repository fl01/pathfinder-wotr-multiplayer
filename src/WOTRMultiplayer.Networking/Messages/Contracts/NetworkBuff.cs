using System;
using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkBuff
    {
        [ProtoMember(1)]
        [LogMe]
        public string Id { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public string BlueprintId { get; set; }

        [ProtoMember(3)]
        [LogMe]
        public string Name { get; set; }

        [ProtoMember(4)]
        [LogMe]
        public bool IsPermanent { get; set; }

        [ProtoMember(5)]
        [LogMe]
        public TimeSpan TimeLeft { get; set; }

        [ProtoMember(6)]
        [LogMe]
        public TimeSpan NextTickTime { get; set; }

        [ProtoMember(7)]
        [LogMe]
        public TimeSpan NextResourceSpendingTime { get; set; }

        [ProtoMember(8)]
        [LogMe]
        public string CasterId { get; set; }

        [ProtoMember(9)]
        [LogMe]
        public int Rank { get; set; }

        [ProtoMember(10)]
        [LogMe]
        public string SourceAbilityCasterId { get; set; }

        [ProtoMember(11)]
        public NetworkAbility SourceAbility { get; set; }

        [ProtoMember(12)]
        public NetworkAbilityParams SourceAbilityParams { get; set; }

        [ProtoMember(13)]
        [LogMe]
        public bool IsHidden { get; set; }
    }
}
