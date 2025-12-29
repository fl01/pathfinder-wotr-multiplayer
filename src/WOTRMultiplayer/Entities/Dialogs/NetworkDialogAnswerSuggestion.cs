using System.Collections.Generic;

namespace WOTRMultiplayer.Entities.Dialogs
{
    public class NetworkDialogAnswerSuggestion
    {
        public string AnswerName { get; set; }

        public List<long> Players { get; set; } = [];
    }
}
