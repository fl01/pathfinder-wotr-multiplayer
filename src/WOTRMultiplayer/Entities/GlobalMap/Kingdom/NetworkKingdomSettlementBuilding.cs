using System.Collections.Generic;

namespace WOTRMultiplayer.Entities.GlobalMap.Kingdom
{
    public class NetworkKingdomSettlementBuilding
    {
        public string Id { get; set; }

        public string BlueprintId { get; set; }

        public List<NetworkKingdomSettlementSlot> Slots { get; set; } = [];
    }
}
