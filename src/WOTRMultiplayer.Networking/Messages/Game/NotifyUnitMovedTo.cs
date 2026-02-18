using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyUnitMovedTo)]
    public class NotifyUnitMovedTo
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkUnitMoveTo Movement { get; set; }
    }
}
