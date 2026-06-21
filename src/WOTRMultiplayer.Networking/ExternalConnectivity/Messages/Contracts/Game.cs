using System;

namespace WOTRMultiplayer.Networking.ExternalConnectivity.Messages.Contracts
{
    public class Game
    {
        public string OwnerId { get; set; }

        public string Code { get; set; }

        public bool HasPassword { get; set; }

        public DateTime CreatedAt { get; set; }

        public string IpAddress { get; set; }

        public int Port { get; set; }
    }
}
