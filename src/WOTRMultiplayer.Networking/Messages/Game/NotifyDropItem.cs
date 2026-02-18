using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyDropItem)]
    public class NotifyDropItem
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkDropItem Drop { get; set; }
    }
}
