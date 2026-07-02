using System.Reflection;
using System.Text.Json;
using WOTRMultiplayer.Networking.ExternalConnectivity;

namespace WOTRMultiplayer.Networking.Abstractions.ExternalConnections
{
    public interface IExternalMessageRegistry
    {
        void Register(Assembly[] assemblies);

        ExternalMessageAttribute GetMessageMetadata(object message);

        object Deserialize(MessageType messageType, int version, JsonElement data);
    }
}
