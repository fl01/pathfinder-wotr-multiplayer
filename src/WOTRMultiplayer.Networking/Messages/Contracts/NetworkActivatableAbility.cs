using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkActivatableAbility
    {
        [ProtoMember(1)]
        public string Id { get; set; }

        [ProtoMember(2)]
        public string BlueprintId { get; set; }

        [ProtoMember(3)]
        public int ShifterFuryIndex { get; set; }

        [ProtoMember(4)]
        public string Name { get; set; }

        [ProtoMember(5)]
        public string CasterId { get; set; }

        [ProtoMember(6)]
        public string TargetId { get; set; }

        [ProtoMember(7)]
        public bool IsActive { get; set; }
    }
}
