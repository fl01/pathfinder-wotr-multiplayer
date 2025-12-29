using System.Collections.Generic;
using WOTRMultiplayer.Entities;

namespace WOTRMultiplayer.Entities.Combat
{
    public class NetworkAIAction
    {
        public string UnitId { get; set; }

        public string TargetId { get; set; }

        public string ActionBlueprintId { get; set; }

        public string ActionType { get; set; }

        public bool IsAutoUseAbility { get; set; }

        public List<NetworkVector3> BestPath { get; set; } = [];

        public bool BestEnableFiveFootStep { get; set; }
    }
}
