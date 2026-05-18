namespace WOTRMultiplayer.Entities
{
    public class NetworkCharacter
    {
        public string Portrait { get; set; }

        public string Name { get; set; }

        public NetworkPlayer Owner { get; set; }

        public string UnitId { get; set; }

        public string CustomPortraitId { get; set; }

        public int? Index { get; set; }
    }
}
