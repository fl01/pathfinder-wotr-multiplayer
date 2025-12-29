using Kingmaker.EntitySystem.Stats;

namespace WOTRMultiplayer.Entities.Inspect
{
    public class NetworkInspectionKnowledgeCheck
    {
        public string TargetUnitId { get; set; }

        public string InitiatorUnitId { get; set; }

        public StatType StatType { get; set; }

        public int DC { get; set; }

        public string InspectionBlueprintId { get; set; }
    }
}
