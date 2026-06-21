namespace WOTRMultiplayer.Networking.ExternalConnectivity.Messages
{
    [ExternalMessage(MessageType.CreateGame, 1)]
    public class CreateGameMessage
    {
        public int Port { get; set; }

        public string Password { get; set; }
    }
}
