using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkGlobalMapTravel
    {
        [ProtoMember(1)]
        [LogMe]
        public string Type { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public NetworkGlobalMapTraveler Traveler { get; set; }

        [ProtoMember(3)]
        [LogMe]
        public NetworkGlobalMapLocation Destination { get; set; }

        [ProtoMember(4)]
        [LogMe]
        public bool FromClick { get; set; }
    }
}
