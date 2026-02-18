using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Lobby
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Lobby.GameServerConnectionSucceeded)]
    public class GameServerConnectionSucceeded
    {
        [ProtoMember(1)]
        [LogMe]
        public long ClientPlayerId { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public NetworkGameSettings GameSettings { get; set; }

        [ProtoMember(3)]
        [LogMe]
        public int SessionSeed { get; set; }
    }
}
