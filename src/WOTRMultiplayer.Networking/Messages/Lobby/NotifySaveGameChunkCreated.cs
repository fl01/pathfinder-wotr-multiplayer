using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Lobby
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Lobby.NotifySaveGameChunkCreated)]
    [ExcludeFromLogging]
    public class NotifySaveGameChunkCreated : IForwardableMessage
    {
        [ProtoMember(1)]
        public int ChunkNumber { get; set; }

        [ProtoMember(2)]
        public byte[] Content { get; set; }
    }
}
