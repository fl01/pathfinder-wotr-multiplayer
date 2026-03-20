using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyItemDescriptionRead)]
    public class NotifyItemDescriptionRead : IForwardableMessage
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkItem Item { get; set; }
    }
}
