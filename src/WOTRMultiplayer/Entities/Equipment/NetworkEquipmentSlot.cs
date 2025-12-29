namespace WOTRMultiplayer.Entities.Equipment
{
    public class NetworkEquipmentSlot
    {
        public NetworkEquipmentSlotPosition Position { get; set; }

        public NetworkItem Item { get; set; }

        public string OwnerId { get; set; }
    }
}
