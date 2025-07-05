using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace WOTRMultiplayer.MP.Entities
{
    public class NetworkDialog
    {
        public string Name { get; set; }

        public string CurrentCueName { get; set; }

        public NetworkDialogAnswer Answer { get; set; }

        public ConcurrentDictionary<string, HashSet<long>> CueViews { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public ConcurrentDictionary<long, string> AnswerSuggestions { get; set; } = new();

        public NetworkDialog(string name)
        {
            Name = name;
        }
    }
}
