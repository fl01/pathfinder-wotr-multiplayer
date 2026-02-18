using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkAbility
    {
        [ProtoMember(1)]
        [LogMe]
        public string Id { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public string BlueprintId { get; set; }

        [ProtoMember(3)]
        [LogMe]
        public string SpellbookId { get; set; }

        [ProtoMember(4)]
        [LogMe]
        public string Name { get; set; }

        [ProtoMember(5)]
        [LogMe]
        public string ConvertedFromId { get; set; }

        [ProtoMember(6)]
        public int SpellLevel { get; set; }

        [ProtoMember(7)]
        public int? Metamagic { get; set; }

        [ProtoMember(8)]
        public int? ParamSpellLevel { get; set; }

        [ProtoMember(9)]
        public string ParamSpellBookId { get; set; }

        [ProtoMember(10)]
        public NetworkAbilityParamSpellSlot ParamSpellSlot { get; set; }
    }
}
