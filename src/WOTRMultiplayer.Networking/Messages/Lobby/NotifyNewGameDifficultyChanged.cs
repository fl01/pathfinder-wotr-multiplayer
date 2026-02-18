using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Lobby
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Lobby.NotifyNewGameDifficultyChanged)]
    public class NotifyNewGameDifficultyChanged
    {
        [ProtoMember(1)]
        [LogMe]
        public string Difficulty { get; set; }
    }
}
