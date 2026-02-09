using System.Collections.Generic;
using ProtoBuf;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.ClientCombatPreparationStarted)]
    public class ClientCombatPreparationStarted
    {
        [ProtoMember(1)]
        public List<NetworkUnit> Units { get; set; } = [];
    }
}
