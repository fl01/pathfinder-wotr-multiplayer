using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Lobby
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Lobby.ClientGameServerConnectionConfirmed)]
    public class ClientGameServerConnectionConfirmed
    {
        [ProtoMember(1)]
        [LogMe]
        public string PlayerName { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public NetworkContentState ContentState { get; set; }
    }
}
