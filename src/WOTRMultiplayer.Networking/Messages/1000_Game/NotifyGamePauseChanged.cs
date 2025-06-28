using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType(1003)]
    public class NotifyGamePauseChanged
    {
        [ProtoMember(1)]
        public bool IsPaused { get; set; }
    }
}
