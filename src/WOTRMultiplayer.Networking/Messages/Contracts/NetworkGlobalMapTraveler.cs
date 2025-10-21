using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkGlobalMapTraveler
    {
        [ProtoMember(1)]
        public NetworkGlobalMapPosition Position { get; set; }
    }
}
