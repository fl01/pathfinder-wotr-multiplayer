using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkGlobalMapPosition
    {
        [ProtoMember(1)]
        public float Edge { get; set; }
    }
}
