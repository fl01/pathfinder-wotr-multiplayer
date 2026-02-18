using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyTacticalUnitUseAbilityCommandExecuted)]
    public class NotifyTacticalUnitUseAbilityCommandExecuted
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkTacticalUnitUseAbilityCommand Command { get; set; }
    }
}
