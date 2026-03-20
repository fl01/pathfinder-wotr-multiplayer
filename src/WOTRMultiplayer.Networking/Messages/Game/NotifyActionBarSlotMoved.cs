using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyActionBarSlotMoved)]
    public class NotifyActionBarSlotMoved : IForwardableMessage
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkActionBarSlot SourceActionBarSlot { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public NetworkActionBarSlot TargetActionBarSlot { get; set; }
    }
}
