using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [MessageType((int)MessageTypes.Game.NotifyTacticalCombatAccelerationChanged)]
    public class NotifyTacticalCombatAccelerationChanged : IForwardableMessage
    {
        [ProtoMember(1)]
        [LogMe]
        public bool IsAccelerated { get; set; }
    }
}
