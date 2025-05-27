using Microsoft.Extensions.DependencyInjection;
using WOTRMultiplayer.Networking.Abstractions;
using WOTRMultiplayer.Networking.Endpoint;

namespace WOTRMultiplayer.Networking.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static ServiceCollection ConfigureNetworking(this ServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton<IIPEndPointParser, IPEndPointParser>();

            serviceCollection.AddScoped<INetworkServer, NetworkServer>();
            serviceCollection.AddScoped<INetworkServerClient, NetworkServerClient>();
            return serviceCollection;
        }
    }
}
