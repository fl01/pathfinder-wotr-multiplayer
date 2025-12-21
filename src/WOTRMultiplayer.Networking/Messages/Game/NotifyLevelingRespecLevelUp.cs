using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyLevelingRespecLevelUp)]
    public class NotifyLevelingRespecLevelUp
    {
        [ProtoMember(1)]
        public long PlayerId { get; set; }
    }
}
