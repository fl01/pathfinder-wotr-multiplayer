using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkActivatableAbility
    {
        [ProtoMember(1)]
        [LogMe]
        public string Id { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public string BlueprintId { get; set; }

        [ProtoMember(3)]
        [LogMe]
        public int ShifterFuryIndex { get; set; }

        [ProtoMember(4)]
        [LogMe]
        public string Name { get; set; }

        [ProtoMember(5)]
        [LogMe]
        public string CasterId { get; set; }

        [ProtoMember(6)]
        [LogMe]
        public string TargetId { get; set; }

        [ProtoMember(7)]
        [LogMe]
        public bool IsActive { get; set; }
    }
}
