using ProtoBuf;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyLevelingSkillPointIncreased)]
    public class NotifyLevelingSkillPointIncreased
    {
        [ProtoMember(1)]
        public NetworkLevelingSkillPoint Skill { get; set; }
    }
}
