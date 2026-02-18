using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Lobby
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Lobby.NotifyLobbySaveGameChanged)]
    public class NotifyLobbySaveGameChanged
    {
        [ProtoMember(1)]
        [LogMe]
        public byte[] Content { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public string GameId { get; set; }

        [ProtoMember(3)]
        [LogMe]
        public int Seed { get; set; }
    }
}
