using System.Collections.Generic;
using ProtoBuf;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Lobby
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Lobby.NotifyCharactersOwnerChanged)]
    public class NotifyCharactersOwnerChanged
    {
        [ProtoMember(1)]
        public List<NetworkCharacterOwner> Owners { get; set; } = [];
    }
}
