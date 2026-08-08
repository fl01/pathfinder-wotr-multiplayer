using Kingmaker.Blueprints.Classes.Spells;
using Kingmaker.EntitySystem.Stats;

namespace WOTRMultiplayer.Entities.SpellbookManagement
{
    public class NetworkMagicHackData
    {
        public string Name { get; set; }

        public SpellSchool SpellSchool { get; set; }

        public SpellTargetType TargetType { get; set; }

        public SavingThrowType ThrowType { get; set; }

        public string LeftSpellBlueprintId { get; set; }

        public string RightSpellBlueprintId { get; set; }

        public string DeliverBlueprintId { get; set; }

        public int SpellLevel { get; set; }

        public bool IsTouch { get; set; }

        public string AdditionalAoeBlueprint { get; set; }
    }
}
