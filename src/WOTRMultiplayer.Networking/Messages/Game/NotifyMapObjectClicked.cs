using ProtoBuf;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyMapObjectClicked)]
    public class NotifyMapObjectClicked
    {
        [ProtoMember(1)]
        public NetworkClick Click { get; set; }
    }
}
