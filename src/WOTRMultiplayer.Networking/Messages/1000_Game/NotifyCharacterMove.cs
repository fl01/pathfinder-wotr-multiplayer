using ProtoBuf;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType(1001)]
    public class NotifyCharacterMove
    {
        [ProtoMember(1)]
        public NetworkCharacterMove Move { get; set; }
    }
}
