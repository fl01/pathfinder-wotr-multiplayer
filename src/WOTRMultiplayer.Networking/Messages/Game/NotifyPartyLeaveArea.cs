using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyPartyLeaveArea)]
    public class NotifyPartyLeaveArea
    {
        [ProtoMember(1)]
        public string AreaExitId { get; set; }
    }
}
