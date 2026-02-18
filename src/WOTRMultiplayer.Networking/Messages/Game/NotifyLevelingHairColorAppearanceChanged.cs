using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyLevelingHairColorAppearanceChanged)]
    public class NotifyLevelingHairColorAppearanceChanged
    {
        [ProtoMember(1)]
        [LogMe]
        public string TextureName { get; set; }
    }
}
