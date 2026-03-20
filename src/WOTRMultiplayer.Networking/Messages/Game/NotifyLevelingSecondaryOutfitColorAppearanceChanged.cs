using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyLevelingSecondaryOutfitColorAppearanceChanged)]
    public class NotifyLevelingSecondaryOutfitColorAppearanceChanged : IForwardableMessage
    {
        [ProtoMember(1)]
        [LogMe]
        public string TextureName { get; set; }
    }
}
