using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyGlobalMapCrusadeArmyMergeCartClosed)]
    public class NotifyGlobalMapCrusadeArmyMergeCartClosed
    {
        [ProtoMember(1)]
        public long PlayerId { get; set; }
    }
}
