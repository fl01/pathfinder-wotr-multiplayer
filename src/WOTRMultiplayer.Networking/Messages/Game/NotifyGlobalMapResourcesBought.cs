using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyGlobalMapResourcesBought)]
    public class NotifyGlobalMapResourcesBought
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkGlobalMapResourceOrder Order { get; set; }
    }
}
