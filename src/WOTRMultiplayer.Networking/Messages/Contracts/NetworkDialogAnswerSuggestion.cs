using System.Collections.Generic;
using ProtoBuf;
using WOTRMultiplayer.Logging.Attributes;

namespace WOTRMultiplayer.Networking.Messages.Contracts
{
    [ProtoContract]
    public class NetworkDialogAnswerSuggestion
    {
        [ProtoMember(1)]
        [LogMe]
        public string AnswerName { get; set; }

        [ProtoMember(2)]
        [LogMe]
        public List<long> Players { get; set; } = [];
    }
}
