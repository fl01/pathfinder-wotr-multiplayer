namespace WOTRMultiplayer.MP.Entities
{
    public class NetworkUnit
    {
        public string Id { get; set; }

        public NetworkVector3 Position { get; set; }

        public float Orientation { get; set; }

        public bool? Surprising { get; set; }

        public bool? Surprised { get; set; }

        public bool? ActingInSurpriseRound { get; set; }
    }
}
