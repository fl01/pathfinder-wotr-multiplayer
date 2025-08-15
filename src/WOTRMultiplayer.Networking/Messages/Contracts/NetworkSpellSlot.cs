using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkSpellSlot
    {
        [ProtoMember(1)]
        public string SpellId { get; set; }

        [ProtoMember(2)]
        public string SpellName { get; set; }

        [ProtoMember(3)]
        public int SpellLevel { get; set; }

        [ProtoMember(4)]
        public int? Index { get; set; }

        [ProtoMember(5)]
        public string SpellbookId { get; set; }

        [ProtoMember(6)]
        public string Type { get; set; }

        [ProtoMember(7)]
        public string UnitId { get; set; }
    }
}
