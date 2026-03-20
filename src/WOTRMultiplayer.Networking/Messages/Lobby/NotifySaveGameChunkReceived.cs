using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Lobby
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Lobby.NotifySaveGameChunkReceived)]
    [ExcludeFromLogging]
    public class NotifySaveGameChunkReceived
    {
        [ProtoMember(1)]
        public int ChunkNumber { get; set; }
    }
}
