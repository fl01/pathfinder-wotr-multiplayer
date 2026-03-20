using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyRestBanterInterrupted)]
    public class NotifyRestBanterInterrupted : IForwardableMessage
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkRestBanter Banter { get; set; }
    }
}
