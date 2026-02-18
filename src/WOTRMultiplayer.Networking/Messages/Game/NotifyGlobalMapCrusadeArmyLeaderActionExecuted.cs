using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyGlobalMapCrusadeArmyLeaderActionExecuted)]
    public class NotifyGlobalMapCrusadeArmyLeaderActionExecuted
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkGlobalMapArmyLeader Leader { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public string Type { get; set; }
    }
}
