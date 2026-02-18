using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyGlobalMapCrusadeArmyLeaderLevelingSkillSelected)]
    public class NotifyGlobalMapCrusadeArmyLeaderLevelingSkillSelected
    {
        [ProtoMember(1)]
        [LogMe]
        public string Id { get; set; }
    }
}
