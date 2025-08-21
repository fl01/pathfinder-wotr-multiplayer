using System.Collections.Generic;
using ProtoBuf;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyCombatTurnSynchronizationRequired)]
    public class NotifyCombatTurnSynchronizationRequired
    {
        [ProtoMember(1)]
        public List<NetworkUnit> Units { get; set; } = [];

        [ProtoMember(2)]
        public string UnitId { get; set; }
    }
}
