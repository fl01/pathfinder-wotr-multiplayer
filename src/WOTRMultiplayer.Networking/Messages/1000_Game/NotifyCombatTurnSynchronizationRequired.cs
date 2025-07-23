using System.Collections.Generic;
using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType(1021)]
    public class NotifyCombatTurnSynchronizationRequired
    {
        [ProtoMember(1)]
        public List<NetworkUnit> Units { get; set; } = [];
    }
}
