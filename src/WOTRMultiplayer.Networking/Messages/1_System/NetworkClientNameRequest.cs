using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.System
{
    [ProtoContract]
    [BeetleX.Packets.MessageType(1)]
    public class NetworkClientNameRequest
    {
    }
}
