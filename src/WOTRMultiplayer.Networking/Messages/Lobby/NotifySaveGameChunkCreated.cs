using System;
using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Lobby
{
    [ProtoContract]
    [MessageType((int)MessageTypes.Lobby.NotifySaveGameChunkCreated)]
    [ExcludeFromLogging]
    public class NotifySaveGameChunkCreated : IForwardableMessage
    {
        [ProtoMember(1)]
        public int ChunkNumber { get; set; }

        [ProtoMember(2)]
        public ReadOnlyMemory<byte> Content { get; set; }
    }
}
