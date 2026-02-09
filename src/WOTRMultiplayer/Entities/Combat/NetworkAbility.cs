namespace WOTRMultiplayer.Entities.Combat
{
    public class NetworkAbility
    {
        public string Id { get; set; }

        public string BlueprintId { get; set; }

        public string SpellbookId { get; set; }

        public string Name { get; set; }

        public string ConvertedFromId { get; set; }

        public int SpellLevel { get; set; }

        public int? Metamagic { get; set; }

        public int? ParamSpellLevel { get; set; }

        public string ParamSpellBookId { get; set; }

        public NetworkAbilityParamSpellSlot ParamSpellSlot { get; set; }
    }
}
