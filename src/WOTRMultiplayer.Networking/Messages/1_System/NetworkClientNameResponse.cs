using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.System
{
    [ProtoContract]
    [BeetleX.Packets.MessageType(2)]
    public class NetworkClientNameResponse
    {
        [ProtoMember(1)]
        public string Name { get; set; }
    }
}
