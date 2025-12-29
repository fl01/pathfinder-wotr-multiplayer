using WOTRMultiplayer.Entities.Equipment;

namespace WOTRMultiplayer.Entities.Loot
{
    public class NetworkDropItem
    {
        public string OwnerEntityId { get; set; }

        public NetworkItem Item { get; set; }
    }
}
