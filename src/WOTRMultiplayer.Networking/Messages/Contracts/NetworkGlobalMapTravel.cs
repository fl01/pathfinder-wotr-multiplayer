using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkGlobalMapTravel
    {
        [ProtoMember(1)]
        public string Type { get; set; }

        [ProtoMember(2)]
        public NetworkGlobalMapTraveler Traveler { get; set; }

        [ProtoMember(3)]
        public NetworkGlobalMapLocation Destination { get; set; }

        [ProtoMember(4)]
        public bool FromClick { get; set; }
    }
}
