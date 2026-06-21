using System.Text.Json;

namespace WOTRMultiplayer.Networking.ExternalConnectivity.SignalR
{
    public class MessageEnvelope
    {
        public MessageType Type { get; set; }

        public int Version { get; set; }

        public JsonElement Data { get; set; }
    }
}
