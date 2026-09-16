using System.Collections.Generic;
using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [MessageType((int)MessageTypes.Game.NotifyArmyCombatTurnSynchronizationRequired)]
    public class NotifyArmyCombatTurnSynchronizationRequired
    {
        [ProtoMember(1)]
        [LogMe]
        public List<NetworkUnit> Units { get; set; } = [];
    }
}
