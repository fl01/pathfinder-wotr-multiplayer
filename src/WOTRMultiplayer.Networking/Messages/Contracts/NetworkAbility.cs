using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkAbility
    {
        [ProtoMember(1)]
        public string Id { get; set; }

        [ProtoMember(2)]
        public string BlueprintId { get; set; }

        [ProtoMember(3)]
        public string SpellbookId { get; set; }

        [ProtoMember(4)]
        public string Name { get; set; }

        [ProtoMember(5)]
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
