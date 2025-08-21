using ProtoBuf;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyCharacterMove)]
    public class NotifyCharacterMove
    {
        [ProtoMember(1)]
        public NetworkCharacterMove Move { get; set; }
    }
}
