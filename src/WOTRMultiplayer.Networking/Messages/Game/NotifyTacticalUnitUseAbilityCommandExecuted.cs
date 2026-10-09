using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [MessageType((int)MessageTypes.Game.NotifyTacticalUnitUseAbilityCommandExecuted)]
    public class NotifyTacticalUnitUseAbilityCommandExecuted : IForwardableMessage
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkTacticalUnitUseAbilityCommand Command { get; set; }
    }
}
