using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyKingdomExited)]
    public class NotifyKingdomExited : IForwardableMessage
    {
    }
}
