using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyGlobalMapCrusadeArmyInfoShown)]
    public class NotifyGlobalMapCrusadeArmyInfoShown
    {
        [ProtoMember(1)]
        public long PlayerId { get; set; }
    }
}
