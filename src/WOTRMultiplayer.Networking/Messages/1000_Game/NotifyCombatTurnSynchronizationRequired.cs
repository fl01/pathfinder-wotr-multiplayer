using System.Collections.Generic;
using ProtoBuf;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType(1021)]
    public class NotifyCombatTurnSynchronizationRequired
    {
        [ProtoMember(1)]
        public List<NetworkUnit> Units { get; set; } = [];

        [ProtoMember(2)]
        public int Round { get; set; }

        [ProtoMember(3)]
        public string UnitId { get; set; }
    }
}
