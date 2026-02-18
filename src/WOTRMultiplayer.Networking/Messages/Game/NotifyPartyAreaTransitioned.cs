using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyPartyAreaTransitioned)]
    public class NotifyPartyAreaTransitioned
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkAreaTransition Transition { get; set; }
    }
}
