using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyLevelingAbilityScoreIncreased)]
    public class NotifyLevelingAbilityScoreIncreased : IForwardableMessage
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkLevelingAbilityScore AbilityScore { get; set; }
    }
}
