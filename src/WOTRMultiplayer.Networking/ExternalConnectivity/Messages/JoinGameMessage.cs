namespace WOTRMultiplayer.Networking.ExternalConnectivity.Messages
{
    [ExternalMessage(MessageType.JoinGame, 1)]
    public class JoinGameMessage
    {
        public string Code { get; set; }

        public string Password { get; set; }
    }
}
