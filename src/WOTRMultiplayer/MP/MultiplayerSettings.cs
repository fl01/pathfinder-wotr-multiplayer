using System;
using System.Linq;

namespace WOTRMultiplayer.MP
{
    public class MultiplayerSettings
    {
        public string PlayerName { get; set; } = Guid.NewGuid().ToString().Split('-').First();

        public string NetworkInterfaceBinding { get; set; }

        public int Port { get; set; }
    }
}
