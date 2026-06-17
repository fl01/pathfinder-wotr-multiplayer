using System;

namespace WOTRMultiplayer.Networking.Configuration
{
    public class NetworkServerConfiguration
    {
        public string Host { get; set; }

        public bool UseIPv6 { get; set; }

        public int PortRangeStart { get; set; }

        public int PortRangeEnd { get; set; }

        public TimeSpan AwaiterTimeout { get; set; }
    }
}
