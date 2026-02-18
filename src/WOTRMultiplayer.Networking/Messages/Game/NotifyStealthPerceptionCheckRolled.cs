using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyStealthPerceptionCheckRolled)]
    public class NotifyStealthPerceptionCheckRolled
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkStealthPerceptionCheck Check { get; set; }
    }
}
