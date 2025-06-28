using System.Collections.Generic;
using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Lobby
{
    [ProtoContract]
    [BeetleX.Packets.MessageType(103)]
    public class NotifyGameCharactersChanged
    {
        [ProtoMember(1)]
        public List<NetworkCharacter> Characters { get; set; } = [];
    }
}
