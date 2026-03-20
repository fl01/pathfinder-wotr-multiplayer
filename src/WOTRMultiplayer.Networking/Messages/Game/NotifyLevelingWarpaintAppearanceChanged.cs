using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyLevelingWarpaintAppearanceChanged)]
    public class NotifyLevelingWarpaintAppearanceChanged : IForwardableMessage
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkLevelingWarpaint Warpaint { get; set; }
    }
}
