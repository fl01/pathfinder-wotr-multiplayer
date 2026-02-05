using ProtoBuf;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyTrapDisarmRolled)]
    public class NotifyTrapDisarmRolled
    {
        [ProtoMember(1)]
        public NetworkTrapDisarm TrapDisarm { get; set; }
    }
}
