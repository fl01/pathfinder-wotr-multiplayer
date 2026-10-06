using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [MessageType((int)MessageTypes.Game.ClientDialogCueAnswerSelected)]
    public class ClientDialogCueAnswerSelected
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

        [ProtoMember(5)]
        [LogMe]
        public bool IsExitAnswer { get; set; }
    }
}
