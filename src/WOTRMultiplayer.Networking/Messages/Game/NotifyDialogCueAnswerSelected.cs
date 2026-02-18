using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyDialogCueAnswerSelected)]
    public class NotifyDialogCueAnswerSelected
    {
        [ProtoMember(1)]
        [LogMe]
        public string CueName { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public NetworkDialog Dialog { get; set; }

        [ProtoMember(3)]
        [LogMe]
        public string AnswerName { get; set; }

        [ProtoMember(4)]
        [LogMe]
        public string ManualUnitSelectionId { get; set; }
    }
}
