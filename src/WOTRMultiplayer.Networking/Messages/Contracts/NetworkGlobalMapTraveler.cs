using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkGlobalMapTraveler
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkGlobalMapPosition Position { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public float? MovementPoints { get; set; }
    }
}
