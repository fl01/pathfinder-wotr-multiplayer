using System.Collections.Generic;
using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType(1023)]
    public class NotifyCombatInitialized
    {
        [ProtoMember(1)]
        public List<NetworkUnit> Units { get; set; } = [];

        [ProtoMember(2)]
        public List<string> UnitsCombatOrder { get; set; } = [];
    }
}
