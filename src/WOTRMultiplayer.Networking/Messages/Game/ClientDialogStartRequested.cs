using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.ClientDialogStartRequested)]
    public class ClientDialogStartRequested
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkDialog Dialog { get; set; }
    }
}
