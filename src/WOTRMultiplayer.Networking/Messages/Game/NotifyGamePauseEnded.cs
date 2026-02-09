using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyGamePauseEnded)]
    public class NotifyGamePauseEnded
    {
        [ProtoMember(1)]
        public int? AreaSeed { get; set; }
    }
}
