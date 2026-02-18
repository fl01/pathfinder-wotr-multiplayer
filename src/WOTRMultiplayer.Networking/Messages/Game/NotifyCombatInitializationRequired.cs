using System.Collections.Generic;
using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyCombatInitializationRequired)]
    public class NotifyCombatInitializationRequired
    {
        [ProtoMember(1)]
        public NetworkCombatState State { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public int CombatSeed { get; set; }

        [ProtoMember(3)]
        [LogMe]
        public List<NetworkAreaEffect> TriggeredAreaEffects { get; set; } = [];
    }
}
