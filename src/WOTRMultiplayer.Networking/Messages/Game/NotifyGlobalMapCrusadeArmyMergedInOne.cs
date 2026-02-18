using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyGlobalMapCrusadeArmyMergedInOne)]
    public class NotifyGlobalMapCrusadeArmyMergedInOne
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkGlobalMapArmySquadSlot SquadSlot { get; set; }
    }
}
