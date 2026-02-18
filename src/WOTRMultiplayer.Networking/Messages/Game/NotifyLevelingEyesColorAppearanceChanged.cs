using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyLevelingEyesColorAppearanceChanged)]
    public class NotifyLevelingEyesColorAppearanceChanged
    {
        [ProtoMember(1)]
        [LogMe]
        public string TextureName { get; set; }
    }
}
