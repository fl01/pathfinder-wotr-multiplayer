using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace WOTRMultiplayer.Entities.Dialogs
{
    public class NetworkDialogState
    {
        public NetworkDialog Dialog { get; }

        public string CurrentCueName { get; set; }

        public NetworkDialogAnswer Answer { get; set; }

        public ConcurrentDictionary<string, HashSet<long>> CueViews { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public ConcurrentDictionary<long, string> AnswerSuggestions { get; set; } = new();

        public bool IsSelectingAnswer { get; set; }

        public bool SystemAnswerEnabled { get; set; }

        public NetworkDialogState(NetworkDialog networkDialog)
        {
            Dialog = networkDialog;
        }
    }
}
