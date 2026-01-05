using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyRestEnded)]
    public class NotifyRestEnded
    {
        [ProtoMember(1)]
        public long PlayerId { get; set; }
    }
}
