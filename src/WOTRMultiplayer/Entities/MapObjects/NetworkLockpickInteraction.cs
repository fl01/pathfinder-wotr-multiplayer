using System.Collections.Generic;
using Kingmaker.UI.MVVM._VM.Lockpick;

namespace WOTRMultiplayer.Entities.MapObjects
{
    public class NetworkLockpickInteraction
    {
        public NetworkMapObject MapObject { get; set; }

        public List<string> Units { get; set; }

        public LockpickType LockpickType { get; set; }
    }
}
