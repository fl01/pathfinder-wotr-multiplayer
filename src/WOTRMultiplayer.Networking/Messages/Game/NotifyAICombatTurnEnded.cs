using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyAICombatTurnEnded)]
    public class NotifyAICombatTurnEnded
    {
        [ProtoMember(1)]
        public string UnitId { get; set; }
    }
}
