using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyGlobalMapRecruitmentClosed)]
    public class NotifyGlobalMapRecruitmentClosed
    {
        [ProtoMember(1)]
        public long PlayerId { get; set; }
    }
}
