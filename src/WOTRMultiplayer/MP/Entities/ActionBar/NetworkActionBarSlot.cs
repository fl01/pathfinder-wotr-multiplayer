using WOTRMultiplayer.MP.Entities.Combat;
using WOTRMultiplayer.MP.Entities.Equipment;

namespace WOTRMultiplayer.MP.Entities.ActionBar
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
