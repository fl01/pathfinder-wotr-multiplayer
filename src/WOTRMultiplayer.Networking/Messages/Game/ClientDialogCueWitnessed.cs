using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.ClientDialogCueWitnessed)]
    public class ClientDialogCueWitnessed
    {
        [ProtoMember(1)]
        [LogMe]
        public string CueName { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public NetworkDialog Dialog { get; set; }
    }
}
