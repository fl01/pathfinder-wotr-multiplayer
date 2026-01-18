using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyCrusadeArmyBattleResultsShown)]
    public class NotifyCrusadeArmyBattleResultsShown
    {
        [ProtoMember(1)]
        public long PlayerId { get; set; }
    }
}
