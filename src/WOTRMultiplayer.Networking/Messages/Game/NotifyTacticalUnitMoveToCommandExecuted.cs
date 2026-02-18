using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyTacticalUnitMoveToCommandExecuted)]
    public class NotifyTacticalUnitMoveToCommandExecuted
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkTacticalUnitMoveToCommand Command { get; set; }
    }
}
