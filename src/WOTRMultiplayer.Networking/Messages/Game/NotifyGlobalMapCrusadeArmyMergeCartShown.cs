using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyGlobalMapCrusadeArmyMergeCartShown)]
    public class NotifyGlobalMapCrusadeArmyMergeCartShown
    {
        [ProtoMember(1)]
        public long PlayerId { get; set; }
    }
}
