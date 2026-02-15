namespace WOTRMultiplayer.Entities.Combat
{
    public class NetworkAIAction
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public string UnitId { get; set; }

        public string TargetId { get; set; }

        public NetworkAIDecisionContext DecisionContext { get; set; }

        public string ActionType { get; set; }

        public bool UseCommand { get; set; }
    }
}
