using ProtoBuf;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyLevelingAbilityScoreIncreased)]
    public class NotifyLevelingAbilityScoreIncreased
    {
        [ProtoMember(1)]
        public NetworkLevelingAbilityScore AbilityScore { get; set; }
    }
}
