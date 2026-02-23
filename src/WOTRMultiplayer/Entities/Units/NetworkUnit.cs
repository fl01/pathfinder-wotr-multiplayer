using WOTRMultiplayer.Entities.Units.Parts;

namespace WOTRMultiplayer.Entities.Units
{
    public class NetworkUnit
    {
        public string Id { get; set; }

        public NetworkVector3 Position { get; set; }

        public float Orientation { get; set; }

        public NetworkUnitTurnBasedInfo TurnBasedInfo { get; set; }

        public NetworkUnitCombatState CombatState { get; set; }

        public NetworkUnitDescriptor Descriptor { get; set; }

        public NetworkUnitBuffCollection BuffCollection { get; set; }

        public NetworkUnitPartKineticist UnitPartKineticist { get; set; }

        public override bool Equals(object obj)
        {
            return obj is NetworkUnit other && string.Equals(this.Id, other.Id, System.StringComparison.OrdinalIgnoreCase);
        }

        public override int GetHashCode()
        {
            return (Id ?? string.Empty).GetHashCode();
        }
    }
}
