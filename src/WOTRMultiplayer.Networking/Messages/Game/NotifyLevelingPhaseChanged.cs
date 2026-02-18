using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyLevelingPhaseChanged)]
    public class NotifyLevelingPhaseChanged
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkLevelingPhase Phase { get; set; }
    }
}
