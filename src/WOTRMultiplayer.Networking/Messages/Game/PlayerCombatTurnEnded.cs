using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.PlayerCombatTurnEnded)]
    public class PlayerCombatTurnEnded
    {
        [ProtoMember(1)]
        public string UnitId { get; set; }
    }
}
