using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyLevelingAbilityScoreDecreased)]
    public class NotifyLevelingAbilityScoreDecreased
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkLevelingAbilityScore AbilityScore { get; set; }
    }
}
