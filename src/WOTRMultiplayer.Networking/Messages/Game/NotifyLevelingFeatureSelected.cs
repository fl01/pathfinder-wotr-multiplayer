using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyLevelingFeatureSelected)]
    public class NotifyLevelingFeatureSelected : IForwardableMessage
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkLevelingFeature Feature { get; set; }
    }
}
