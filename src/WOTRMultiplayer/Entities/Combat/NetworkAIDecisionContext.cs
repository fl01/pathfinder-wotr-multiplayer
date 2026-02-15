using System.Collections.Generic;

namespace WOTRMultiplayer.Entities.Combat
{
    public class NetworkAIDecisionContext
    {
        public List<NetworkVector3> VectorPath { get; set; } = [];

        public bool BestEnableFiveFootStep { get; set; }
    }
}
