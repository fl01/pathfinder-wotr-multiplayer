namespace WOTRMultiplayer.Entities
{
    public class NetworkCharacter
    {
        public string Portrait { get; set; }

        public string Name { get; set; }

        public NetworkPlayer Owner { get; set; }

        public string UnitId { get; set; }
    }
}
