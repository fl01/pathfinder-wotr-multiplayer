using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.GamePauseChanged)]
    public class GamePauseChanged
    {
        [ProtoMember(1)]
        public bool IsPaused { get; set; }
    }
}
