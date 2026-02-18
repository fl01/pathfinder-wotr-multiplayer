using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyCharacterMove)]
    public class NotifyCharacterMove
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkCharacterMove Move { get; set; }
    }
}
