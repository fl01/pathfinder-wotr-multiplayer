namespace WOTRMultiplayer.Entities
{
    public class NetworkTargetWrapper
    {
        public NetworkVector3 Point { get; set; }

        public float? Orientation { get; set; }

        public string UnitUniqueId { get; set; }

        public NetworkTargetWrapper(NetworkVector3 point, float? orientation, string unitUniqueId)
        {
            Point = point;
            Orientation = orientation;
            UnitUniqueId = unitUniqueId;
        }

        public NetworkTargetWrapper()
        {
        }
    }
}
