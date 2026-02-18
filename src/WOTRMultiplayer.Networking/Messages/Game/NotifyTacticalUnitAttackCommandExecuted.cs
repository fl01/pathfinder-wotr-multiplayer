using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyTacticalUnitAttackCommandExecuted)]
    public class NotifyTacticalUnitAttackCommandExecuted
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkTacticalUnitAttackCommand Command { get; set; }
    }
}
