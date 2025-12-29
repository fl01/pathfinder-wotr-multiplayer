using WOTRMultiplayer.Entities.Combat;
using WOTRMultiplayer.Entities.Equipment;

namespace WOTRMultiplayer.Entities.ActionBar
{
    public class NetworkActionBarSlot
    {
        public int Index { get; set; }

        public string UnitId { get; set; }

        public NetworkItem Item { get; set; }

        public NetworkAbility Ability { get; set; }

        public NetworkActivatableAbility ActivatableAbility { get; set; }
    }
}
