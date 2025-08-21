namespace WOTRMultiplayer.MP.Entities.Movement
{
    public class NetworkCharacterMove
    {
        public string UnitId { get; set; }

        public NetworkVector3 Destination { get; set; }

        public float Delay { get; set; }

        public float Orientation { get; set; }
    }
}
