using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyLevelingBirthMonthChanged)]
    public class NotifyLevelingBirthMonthChanged
    {
        [ProtoMember(1)]
        public string Direction { get; set; }
    }
}
