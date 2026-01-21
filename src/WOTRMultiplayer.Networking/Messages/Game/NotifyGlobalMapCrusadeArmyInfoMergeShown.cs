using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyGlobalMapCrusadeArmyInfoMergeShown)]
    public class NotifyGlobalMapCrusadeArmyInfoMergeShown
    {
        [ProtoMember(1)]
        public long PlayerId { get; set; }
    }
}
