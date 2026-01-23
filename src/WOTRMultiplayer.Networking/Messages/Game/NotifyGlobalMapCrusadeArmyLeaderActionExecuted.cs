using ProtoBuf;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyGlobalMapCrusadeArmyLeaderActionExecuted)]
    public class NotifyGlobalMapCrusadeArmyLeaderActionExecuted
    {
        [ProtoMember(1)]
        public NetworkGlobalMapArmyLeader Leader { get; set; }

        [ProtoMember(2)]
        public string Type { get; set; }
    }
}
