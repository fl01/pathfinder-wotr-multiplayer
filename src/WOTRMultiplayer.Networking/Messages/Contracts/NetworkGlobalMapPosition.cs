using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkGlobalMapPosition
    {
        [ProtoMember(1)]
        public float EdgePosition { get; set; }
    }
}
