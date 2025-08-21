using System.Collections.Generic;
using ProtoBuf;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Lobby
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Lobby.NotifyPlayersChanged)]
    public class NotifyPlayersChanged
    {
        [ProtoMember(1)]
        public List<NetworkPlayer> Players { get; set; } = [];
    }
}
