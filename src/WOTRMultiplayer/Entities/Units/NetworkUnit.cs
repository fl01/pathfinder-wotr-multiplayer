using WOTRMultiplayer.Entities.Combat;

namespace WOTRMultiplayer.Entities.Units
{
    public class NetworkUnit
    {
        public string Id { get; set; }

        public NetworkVector3 Position { get; set; }

        public float Orientation { get; set; }

        public NetworkUnitTurnBasedInfo TurnBasedInfo { get; set; }

        public NetworkUnitCombatState CombatState { get; set; }

        public NetworkUnitAttack CurrentAttack { get; set; }

        public NetworkAbility CurrentAbility { get; set; }
    }
}
