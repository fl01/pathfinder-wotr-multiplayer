using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [MessageType((int)MessageTypes.Game.NotifyCrusadeArmyBattleResultsClosed)]
    public class NotifyCrusadeArmyBattleResultsClosed : IForwardableMessage
    {
    }
}
