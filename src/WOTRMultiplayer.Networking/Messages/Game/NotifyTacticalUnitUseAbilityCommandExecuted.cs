using ProtoBuf;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyTacticalUnitUseAbilityCommandExecuted)]
    public class NotifyTacticalUnitUseAbilityCommandExecuted
    {
        [ProtoMember(1)]
        public NetworkTacticalUnitUseAbilityCommand Command { get; set; }
    }
}
