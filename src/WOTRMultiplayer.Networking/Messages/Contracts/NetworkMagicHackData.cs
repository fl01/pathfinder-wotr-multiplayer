using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkMagicHackData
    {
        [ProtoMember(1)]
        [LogMe]
        public string Name { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public string SpellSchool { get; set; }

        [ProtoMember(3)]
        [LogMe]
        public string TargetType { get; set; }

        [ProtoMember(4)]
        [LogMe]
        public string ThrowType { get; set; }

        [ProtoMember(5)]
        [LogMe]
        public string LeftSpellBlueprintId { get; set; }

        [ProtoMember(6)]
        [LogMe]
        public string RightSpellBlueprintId { get; set; }

        [ProtoMember(7)]
        public string DeliverBlueprintId { get; set; }

        [ProtoMember(8)]
        public int SpellLevel { get; set; }

        [ProtoMember(9)]
        public bool IsTouch { get; set; }

        [ProtoMember(10)]
        public string AdditionalAoeBlueprint { get; set; }
    }
}
