namespace WOTRMultiplayer.Networking.ExternalConnectivity.Messages
{
    [ExternalMessage(MessageType.BeginConnecting, 1)]
    public class BeginConnectingMessage
    {
        public string GameCode { get; set; }

        public string SessionId { get; set; }

        public int Port { get; set; }
    }
}
