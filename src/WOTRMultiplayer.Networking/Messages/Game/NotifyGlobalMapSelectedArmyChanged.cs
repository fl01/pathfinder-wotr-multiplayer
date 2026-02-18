using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyGlobalMapSelectedArmyChanged)]
    public class NotifyGlobalMapSelectedArmyChanged
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkGlobalMapArmy Army { get; set; }
    }
}
