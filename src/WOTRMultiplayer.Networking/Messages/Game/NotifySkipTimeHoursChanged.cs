using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifySkipTimeHoursChanged)]
    public class NotifySkipTimeHoursChanged
    {
        [ProtoMember(1)]
        [LogMe]
        public float Hours { get; set; }
    }
}
