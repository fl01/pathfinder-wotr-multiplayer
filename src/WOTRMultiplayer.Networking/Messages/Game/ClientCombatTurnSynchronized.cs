using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.ClientCombatTurnSynchronized)]
    public class ClientCombatTurnSynchronized
    {
        [ProtoMember(1)]
        [LogMe]
        public string UnitId { get; set; }
    }
}
