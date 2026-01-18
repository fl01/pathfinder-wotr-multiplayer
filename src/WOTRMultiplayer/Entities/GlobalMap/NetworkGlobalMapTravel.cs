namespace WOTRMultiplayer.Entities.GlobalMap
{
    public class NetworkGlobalMapTravel
    {
        public NetworkGlobalMapPathType Type { get; set; }

        public NetworkGlobalMapTraveler Traveler { get; set; }

        public NetworkGlobalMapLocation Destination { get; set; }

        public bool FromClick { get; set; }
    }
}
