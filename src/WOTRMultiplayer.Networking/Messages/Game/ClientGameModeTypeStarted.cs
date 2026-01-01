using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.ClientGameModeTypeStarted)]
    public class ClientGameModeTypeStarted
    {
        [ProtoMember(1)]
        public string Name { get; set; }
    }
}
