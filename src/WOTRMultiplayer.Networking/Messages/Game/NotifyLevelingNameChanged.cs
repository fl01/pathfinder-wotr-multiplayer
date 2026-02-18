using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyLevelingNameChanged)]
    public class NotifyLevelingNameChanged
    {
        [ProtoMember(1)]
        [LogMe]
        public string Name { get; set; }
    }
}
