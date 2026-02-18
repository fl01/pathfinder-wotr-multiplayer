using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyLevelingSkillPointIncreased)]
    public class NotifyLevelingSkillPointIncreased
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkLevelingSkillPoint Skill { get; set; }
    }
}
