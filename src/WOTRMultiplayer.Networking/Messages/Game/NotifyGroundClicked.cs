using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyGroundClicked)]
    public class NotifyGroundClicked
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkClick Click { get; set; }
    }
}
