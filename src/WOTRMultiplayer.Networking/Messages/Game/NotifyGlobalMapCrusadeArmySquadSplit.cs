using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [MessageType((int)MessageTypes.Game.NotifyGlobalMapCrusadeArmySquadSplit)]
    public class NotifyGlobalMapCrusadeArmySquadSplit
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkGlobalMapArmySquadSlot SquadSlot { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public int Count { get; set; }
    }
}
