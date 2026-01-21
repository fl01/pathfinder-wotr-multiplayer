using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyGlobalMapCrusadeArmyInfoMergeClosed)]
    public class NotifyGlobalMapCrusadeArmyInfoMergeClosed
    {
        [ProtoMember(1)]
        public long PlayerId { get; set; }
    }
}
