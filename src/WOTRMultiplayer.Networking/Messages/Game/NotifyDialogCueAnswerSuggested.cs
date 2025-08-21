using System.Collections.Generic;
using ProtoBuf;
using WOTRMultiplayer.Networking.Messages.Contracts;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType((int)MessageTypes.Game.NotifyDialogCueAnswerSuggested)]
    public class NotifyDialogCueAnswerSuggested
    {
        [ProtoMember(1)]
        public string DialogName { get; set; }

        [ProtoMember(2)]
        public string CueName { get; set; }

        [ProtoMember(3)]
        public List<NetworkDialogAnswerSuggestion> Suggestions { get; set; } = [];
    }
}
