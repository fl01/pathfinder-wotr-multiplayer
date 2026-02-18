using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyKingdomEntered)]
    public class NotifyKingdomEntered
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkKingdomEntryPoint EntryPoint { get; set; }
    }
}
