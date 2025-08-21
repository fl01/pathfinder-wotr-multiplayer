using ProtoBuf;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyLevelingPhaseChanged)]
    public class NotifyLevelingPhaseChanged
    {
        [ProtoMember(1)]
        public NetworkLevelingPhase Phase { get; set; }
    }
}
