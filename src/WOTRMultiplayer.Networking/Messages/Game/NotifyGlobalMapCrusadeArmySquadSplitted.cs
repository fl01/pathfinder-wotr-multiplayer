using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyGlobalMapCrusadeArmySquadSplitted)]
    public class NotifyGlobalMapCrusadeArmySquadSplitted
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkGlobalMapArmySquadSlot SquadSlot { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public int Count { get; set; }
    }
}
