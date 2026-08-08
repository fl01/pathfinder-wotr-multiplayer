namespace WOTRMultiplayer.Entities.SpellbookManagement
{
    public class NetworkMagicHackSpell
    {
        public int Index { get; set; }

        public string SpellbookId { get; set; }

        public int SpellLevel { get; set; }

        public string UnitId { get; set; }

        public string AbilityBlueprintId { get; set; }

        public string TouchBlueprintId { get; set; }

        public string DefaultBlueprintId { get; set; }

        public NetworkMagicHackData MagicHackData { get; set; }
    }
}
