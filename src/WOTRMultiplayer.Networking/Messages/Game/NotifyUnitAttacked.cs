using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyUnitAttacked)]
    public class NotifyUnitAttacked : IForwardableMessage
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkUnitAttack Attack { get; set; }
    }
}
