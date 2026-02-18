using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyLevelingFaceAppearanceChanged)]
    public class NotifyLevelingFaceAppearanceChanged
    {
        [ProtoMember(1)]
        [LogMe]
        public int Index { get; set; }
    }
}
