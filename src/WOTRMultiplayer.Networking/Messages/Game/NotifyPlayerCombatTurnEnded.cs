using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.PlayerCombatTurnEnded)]
    public class NotifyPlayerCombatTurnEnded
    {
        [ProtoMember(1)]
        public string UnitId { get; set; }
    }
}
