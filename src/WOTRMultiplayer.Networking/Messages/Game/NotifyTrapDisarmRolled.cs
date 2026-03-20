using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyTrapDisarmRolled)]
    public class NotifyTrapDisarmRolled : IForwardableMessage
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkTrapDisarm TrapDisarm { get; set; }
    }
}
