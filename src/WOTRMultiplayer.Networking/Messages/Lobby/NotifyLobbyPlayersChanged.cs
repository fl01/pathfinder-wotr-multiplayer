using System.Collections.Generic;
using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Lobby
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Lobby.NotifyLobbyPlayersChanged)]
    public class NotifyLobbyPlayersChanged
    {
        [ProtoMember(1)]
        [LogMe]
        public List<NetworkPlayer> Players { get; set; } = [];
    }
}
