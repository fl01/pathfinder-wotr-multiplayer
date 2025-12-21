using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyLevelingRespecWindowShown)]
    public class NotifyLevelingRespecWindowShown
    {
        [ProtoMember(1)]
        public long PlayerId { get; set; }

        [ProtoMember(2)]
        public string UnitId { get; set; }
    }
}
