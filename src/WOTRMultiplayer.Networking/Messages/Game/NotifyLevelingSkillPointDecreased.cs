using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyLevelingSkillPointDecreased)]
    public class NotifyLevelingSkillPointDecreased : IForwardableMessage
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkLevelingSkillPoint Skill { get; set; }
    }
}
