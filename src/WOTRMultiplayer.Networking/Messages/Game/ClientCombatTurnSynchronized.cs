using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.ClientCombatTurnSynchronized)]
    public class ClientCombatTurnSynchronized
    {
        [ProtoMember(1)]
        public string UnitId { get; set; }
    }
}
