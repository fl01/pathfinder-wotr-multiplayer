namespace WOTRMultiplayer.Networking.ExternalConnectivity.Messages
{
    [ExternalMessage(MessageType.TerminateGame, 1)]
    public class TerminateGameMessage
    {
        public string Code { get; set; }
    }
}
