using ProtoBuf;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Lobby
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Lobby.GameServerConnectionSucceeded)]
    public class GameServerConnectionSucceeded
    {
        [ProtoMember(1)]
        public long ClientPlayerId { get; set; }

        [ProtoMember(2)]
        public NetworkGameSettings GameSettings { get; set; }

        [ProtoMember(3)]
        public int RestBanterSeed { get; set; }
    }
}
