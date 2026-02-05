using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Lobby
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Lobby.NotifyLobbySaveGameChanged)]
    public class NotifyLobbySaveGameChanged
    {
        [ProtoMember(1)]
        public byte[] Content { get; set; }

        [ProtoMember(2)]
        public string GameId { get; set; }

        [ProtoMember(3)]
        public int Seed { get; set; }
    }
}
