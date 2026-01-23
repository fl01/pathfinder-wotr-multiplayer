using ProtoBuf;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyGlobalMapCrusadeArmyInfoCartNameChanged)]
    public class NotifyGlobalMapCrusadeArmyInfoCartNameChanged
    {
        [ProtoMember(1)]
        public NetworkGlobalMapArmy Army { get; set; }
    }
}
