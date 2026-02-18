using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyNewGameSequencePhaseChanged)]
    public class NotifyNewGameSequencePhaseChanged
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkNewGameSequencePhase Phase { get; set; }
    }
}
