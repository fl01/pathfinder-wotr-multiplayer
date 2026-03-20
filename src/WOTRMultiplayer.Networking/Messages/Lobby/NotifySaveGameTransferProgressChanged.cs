using System.Collections.Generic;
using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Lobby
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Lobby.NotifySaveGameTransferProgressChanged)]
    [ExcludeFromLogging]
    public class NotifySaveGameTransferProgressChanged
    {
        [ProtoMember(1)]
        [LogMe]
        public List<KeyValuePair<long, int>> Players { get; set; } = [];
    }
}
