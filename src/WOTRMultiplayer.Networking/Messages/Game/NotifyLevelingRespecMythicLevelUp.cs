using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyLevelingRespecMythicLevelUp)]
    public class NotifyLevelingRespecMythicLevelUp
    {
        [ProtoMember(1)]
        public long PlayerId { get; set; }
    }
}
