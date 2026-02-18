using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyLevelingScarAppearanceChanged)]
    public class NotifyLevelingScarAppearanceChanged
    {
        [ProtoMember(1)]
        [LogMe]
        public int Index { get; set; }
    }
}
