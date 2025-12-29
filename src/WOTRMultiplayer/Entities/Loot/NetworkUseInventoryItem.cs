using WOTRMultiplayer.Entities;
using WOTRMultiplayer.Entities.Equipment;

namespace WOTRMultiplayer.Entities.Loot
{
    public class NetworkUseInventoryItem
    {
        public NetworkItem Item { get; set; }

        public string UserUnitId { get; set; }

        public NetworkTargetWrapper Target { get; set; }

        public NetworkEquipmentSlotPosition SlotPosition { get; set; }
    }
}
