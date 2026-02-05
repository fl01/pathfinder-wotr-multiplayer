namespace WOTRMultiplayer.Entities.MapObjects
{
    public class NetworkTrapDisarm
    {
        public NetworkMapObject MapObject { get; set; }

        public int Roll { get; set; }

        public bool IsSuccess { get; set; }

        public string UnitId { get; set; }
    }
}
