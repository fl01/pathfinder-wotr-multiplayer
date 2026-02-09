using WOTRMultiplayer.Entities.Spells;

namespace WOTRMultiplayer.Entities.Combat
{
    public class NetworkAbilityParamSpellSlot
    {
        public string SpellbookId { get; set; }

        public int SpellLevel { get; set; }

        public NetworkSpellSlot Slot { get; set; }
    }
}
