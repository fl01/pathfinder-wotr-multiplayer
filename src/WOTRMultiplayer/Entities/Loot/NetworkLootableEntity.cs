using Kingmaker.EntitySystem;
using WOTRMultiplayer.Entities;

namespace WOTRMultiplayer.Entities.Loot
{
    public class NetworkLootableEntity
    {
        public string Id { get; set; }

        public NetworkVector3 Position { get; set; }

        public NetworkLootableEntityType Type { get; set; }
    }
}
