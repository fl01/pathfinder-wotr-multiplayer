using ProtoBuf;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyLevelingFeatureSelected)]
    public class NotifyLevelingFeatureSelected
    {
        [ProtoMember(1)]
        public NetworkLevelingFeature Feature { get; set; }
    }
}
