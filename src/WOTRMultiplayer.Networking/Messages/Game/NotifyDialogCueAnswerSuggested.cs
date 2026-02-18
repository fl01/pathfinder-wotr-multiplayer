using System.Collections.Generic;
using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyDialogCueAnswerSuggested)]
    public class NotifyDialogCueAnswerSuggested
    {
        [ProtoMember(1)]
        [LogMe]
        public NetworkDialog Dialog { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public string CueName { get; set; }

        [ProtoMember(3)]
        [LogMe]
        public List<NetworkDialogAnswerSuggestion> Suggestions { get; set; } = [];
    }
}
