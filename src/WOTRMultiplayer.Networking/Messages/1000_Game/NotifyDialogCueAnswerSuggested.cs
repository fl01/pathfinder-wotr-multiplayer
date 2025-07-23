using System.Collections.Generic;
using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages.Game
{
    [ProtoContract]
    [BeetleX.Packets.MessageType(1010)]
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
