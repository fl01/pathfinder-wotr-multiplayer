using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyGlobalMapCrusadeArmyLeaderLevelingShown)]
    public class NotifyGlobalMapCrusadeArmyLeaderLevelingShown : IForwardableMessage
    {
        [ProtoMember(1)]
        [LogMe]
        public long PlayerId { get; set; }
    }
}
