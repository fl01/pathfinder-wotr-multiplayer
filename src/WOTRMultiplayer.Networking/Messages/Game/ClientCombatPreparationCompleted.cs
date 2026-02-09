using System.Collections.Generic;
using ProtoBuf;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.ClientCombatPreparationCompleted)]
    public class ClientCombatPreparationCompleted
    {
        [ProtoMember(1)]
        public long PlayerId { get; set; }

        [ProtoMember(2)]
        public List<NetworkUnit> Units { get; set; } = [];
    }
}
