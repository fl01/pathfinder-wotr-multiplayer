namespace WOTRMultiplayer.MP.Entities
{
    public class NetworkActionsState
    {
        public NetworkVector3 ApproachPoint { get; set; }

        public float ApproachRadius { get; set; }

        public NetworkCombatAction Move { get; set; }

        public NetworkCombatAction Swift { get; set; }

        public NetworkCombatAction Standard { get; set; }

        public NetworkCombatAction FiveFootStep { get; set; }

        public NetworkCombatAction Free { get; set; }
    }
}
