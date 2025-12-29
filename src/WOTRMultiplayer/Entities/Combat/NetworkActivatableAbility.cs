namespace WOTRMultiplayer.Entities.Combat
{
    public class NetworkActivatableAbility
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public string CasterId { get; set; }

        public string TargetId { get; set; }

        public bool IsActive { get; set; }
    }
}
