using ProtoBuf;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyTacticalUnitAttackCommandExecuted)]
    public class NotifyTacticalUnitAttackCommandExecuted
    {
        [ProtoMember(1)]
        public NetworkTacticalUnitAttackCommand Command { get; set; }
    }
}
