using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyDialogCueAnswerSelected)]
    public class NotifyDialogCueAnswerSelected
    {
        [ProtoMember(1)]
        public string CueName { get; set; }

        [ProtoMember(2)]
        public string DialogName { get; set; }

        [ProtoMember(3)]
        public string AnswerName { get; set; }

        [ProtoMember(4)]
        public string ManualUnitSelectionId { get; set; }
    }
}
