using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Lobby
{
    [ProtoContract]
    [BeetleX.Packets.MessageType(101)]
    public class PlayerNameResponse
    {
        [ProtoMember(1)]
        public string Name { get; set; }
    }
}
