namespace WOTRMultiplayer.MP.Entities.Combat
{
    public class NetworkAIAction
    {
        public string UnitId { get; set; }

        public decimal CurrentScore { get; set; }

        public string TargetId { get; set; }

        public string ActionBlueprintId { get; set; }

        public string ActionType { get; set; }

        public bool IsAutoUseAbility { get; set; }
    }
}
