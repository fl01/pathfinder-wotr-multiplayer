using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkGlobalMapState
    {
        [ProtoMember(1)]
        public NetworkGlobalMapTraveler Player { get; set; }
    }
}
