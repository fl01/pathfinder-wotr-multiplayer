namespace WOTRMultiplayer.Networking.ExternalConnectivity.Messages
{
    [ExternalMessage(MessageType.GameCreated, 1)]
    public class ErrorMessage
    {
        public string Key { get; set; }
    }
}
