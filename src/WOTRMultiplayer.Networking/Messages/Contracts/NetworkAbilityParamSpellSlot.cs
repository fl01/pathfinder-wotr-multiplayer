using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkAbilityParamSpellSlot
    {
        [ProtoMember(1)]
        public string SpellbookId { get; set; }

        [ProtoMember(2)]
        public int SpellLevel { get; set; }

        [ProtoMember(3)]
        public NetworkSpellSlot Slot { get; set; }
    }
}
