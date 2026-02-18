using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyLevelingBirthDayChanged)]
    public class NotifyLevelingBirthDayChanged
    {
        [ProtoMember(1)]
        [LogMe]
        public string Direction { get; set; }
    }
}
