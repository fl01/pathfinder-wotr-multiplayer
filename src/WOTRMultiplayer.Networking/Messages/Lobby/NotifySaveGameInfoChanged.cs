using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Lobby
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Lobby.NotifySaveGameInfoChanged)]
    public class NotifySaveGameInfoChanged : IForwardableMessage
    {
        [ProtoMember(1)]
        [LogMe]
        public string GameId { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public int Seed { get; set; }

        [ProtoMember(3)]
        [LogMe]
        public int ExpectedChunks { get; set; }

        [ProtoMember(4)]
        [LogMe]
        public int ContentSize { get; set; }

        [ProtoMember(5)]
        [LogMe]
        public bool AutoStart { get; set; }

        [ProtoMember(6)]
        [LogMe]
        public bool IsNewGameSequence { get; set; }
    }
}
