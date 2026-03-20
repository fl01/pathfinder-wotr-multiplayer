using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyUnitInteractedWithUnit)]
    public class NotifyUnitInteractedWithUnit : IForwardableMessage
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkUnitInteractWithUnit Interaction { get; set; }
    }
}
