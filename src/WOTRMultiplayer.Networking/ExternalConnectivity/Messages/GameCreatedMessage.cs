using WOTRMultiplayer.Networking.ExternalConnectivity.Messages.Contracts;

namespace WOTRMultiplayer.Networking.ExternalConnectivity.Messages
{
    [ExternalMessage(MessageType.GameCreated, 1)]
    public class GameCreatedMessage
    {
        public Game Game { get; set; }
    }
}
