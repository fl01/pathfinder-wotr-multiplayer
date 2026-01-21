using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyGlobalMapCrusadeArmyInfoClosed)]
    public class NotifyGlobalMapCrusadeArmyInfoClosed
    {
    }
}
