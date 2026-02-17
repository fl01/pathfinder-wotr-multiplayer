namespace WOTRMultiplayer.Entities.Combat
{
    public class NetworkActivatableAbility
    {
        public string Id { get; set; }

        public string BlueprintId { get; set; }

        public int ShifterFuryIndex { get; set; } = -1;

        public string Name { get; set; }

        public string CasterId { get; set; }

        public string TargetId { get; set; }

        public bool IsActive { get; set; }
    }
}
