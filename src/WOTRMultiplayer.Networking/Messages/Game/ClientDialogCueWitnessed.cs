using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.ClientDialogCueWitnessed)]
    public class ClientDialogCueWitnessed
    {
        [ProtoMember(1)]
        public string CueName { get; set; }

        [ProtoMember(2)]
        public string DialogName { get; set; }
    }
}
