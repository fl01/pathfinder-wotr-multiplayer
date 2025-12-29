using WOTRMultiplayer.Entities;

namespace WOTRMultiplayer.Entities.GlobalMap
{
    public class NetworkGlobalMapEncounter
    {
        public int Seed { get; set; }

        public string BlueprintId { get; set; }

        public string AvoidanceResult { get; set; }

        public NetworkVector3 Position { get; set; }

        public bool IsTrader { get; set; }
    }
}
