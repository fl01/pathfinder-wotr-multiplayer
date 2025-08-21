using ProtoBuf;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyOvertipInteracted)]
    public class NotifyOvertipInteracted
    {
        [ProtoMember(1)]
        public NetworkOvertip Overtip { get; set; }
    }
}
