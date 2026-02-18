using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyLevelingTattooColorAppearanceChanged)]
    public class NotifyLevelingTattooColorAppearanceChanged
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkLevelingTattoo Tattoo { get; set; }
    }
}
