using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyMapObjectLockpicked)]
    public class NotifyMapObjectLockpicked : IForwardableMessage
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkLockpickInteraction LockpickInteraction { get; set; }
    }
}
