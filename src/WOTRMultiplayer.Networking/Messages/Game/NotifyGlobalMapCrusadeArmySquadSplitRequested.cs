using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyGlobalMapCrusadeArmySquadSplitRequested)]
    public class NotifyGlobalMapCrusadeArmySquadSplitRequested
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkGlobalMapArmySquadSlot SourceSquadSlot { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public NetworkGlobalMapArmySquadSlot TargetSquadSlot { get; set; }

        [ProtoMember(3)]
        [LogMe]
        public int Count { get; set; }
    }
}
