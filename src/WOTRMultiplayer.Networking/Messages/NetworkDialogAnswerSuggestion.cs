using System.Collections.Generic;
using ProtoBuf;

namespace WOTRMultiplayer.Networking.Messages
{
    [ProtoContract]
    public class NetworkDialogAnswerSuggestion
    {
        [ProtoMember(1)]
        public string AnswerName { get; set; }

        [ProtoMember(2)]
        public List<long> Players { get; set; } = [];
    }
}
