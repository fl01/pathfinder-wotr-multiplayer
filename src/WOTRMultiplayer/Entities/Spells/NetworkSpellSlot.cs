using Kingmaker.UnitLogic;

namespace WOTRMultiplayer.Entities.Spells
{
    public class NetworkSpellSlot
    {
        public string SpellId { get; set; }

        public string SpellName { get; set; }

        public int SpellLevel { get; set; }

        public int? Index { get; set; }

        public string SpellbookId { get; set; }

        public SpellSlotType? Type { get; set; }

        public string UnitId { get; set; }
    }
}
