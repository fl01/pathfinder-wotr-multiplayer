using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyOvertipInteracted)]
    public class NotifyOvertipInteracted : IForwardableMessage
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkOvertip Overtip { get; set; }
    }
}
