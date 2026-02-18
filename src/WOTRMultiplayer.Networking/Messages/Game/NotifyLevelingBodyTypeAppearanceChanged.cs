using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyLevelingBodyTypeAppearanceChanged)]
    public class NotifyLevelingBodyTypeAppearanceChanged
    {
        [ProtoMember(1)]
        [LogMe]
        public int Index { get; set; }
    }
}
