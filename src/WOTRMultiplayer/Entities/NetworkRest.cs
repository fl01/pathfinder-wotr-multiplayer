using System.Collections.Generic;

namespace WOTRMultiplayer.Entities
{
    public class NetworkRest
    {
        public int SleepPhase { get; set; }

        public List<NetworkRandomEncounter> RandomEncounters { get; set; } = [];

        public HashSet<long> PlayersFinishedRest { get; set; } = [];
    }
}
