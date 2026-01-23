using ProtoBuf;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyGlobalMapResourcesBought)]
    public class NotifyGlobalMapResourcesBought
    {
        [ProtoMember(1)]
        public NetworkGlobalMapResourceOrder Order { get; set; }
    }
}
