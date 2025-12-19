using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyLevelingBirthDayChanged)]
    public class NotifyLevelingBirthDayChanged
    {
        [ProtoMember(1)]
        public string Direction { get; set; }
    }
}
