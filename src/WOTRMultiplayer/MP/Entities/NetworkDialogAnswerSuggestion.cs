using System.Collections.Generic;

namespace WOTRMultiplayer.MP.Entities
{
    public class NetworkDialogAnswerSuggestion
    {
        public string AnswerName { get; set; }

        public List<long> Players { get; set; } = [];
    }
}
