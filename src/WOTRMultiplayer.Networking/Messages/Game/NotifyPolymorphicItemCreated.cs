using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyPolymorphicItemCreated)]
    public class NotifyPolymorphicItemCreated
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkPolymorphicItem PolymorphicItem { get; set; }
    }
}
