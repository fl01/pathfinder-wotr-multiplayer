using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [MessageType((int)MessageTypes.Game.NotifyDialogCueAnswerSelectionDenied)]
    public class NotifyDialogCueAnswerSelectionDenied
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
    }
}
