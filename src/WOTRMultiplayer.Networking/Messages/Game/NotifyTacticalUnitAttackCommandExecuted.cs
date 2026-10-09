using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [MessageType((int)MessageTypes.Game.NotifyTacticalUnitAttackCommandExecuted)]
    public class NotifyTacticalUnitAttackCommandExecuted : IForwardableMessage
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkTacticalUnitAttackCommand Command { get; set; }
    }
}
