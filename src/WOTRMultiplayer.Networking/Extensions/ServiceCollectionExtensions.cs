using Microsoft.Extensions.DependencyInjection;
using WOTRMultiplayer.Networking.Abstractions;

namespace WOTRMultiplayer.Networking.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static ServiceCollection ConfigureNetworking(this ServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton<IIPEndPointParser, IPEndPointParser>();
            serviceCollection.AddSingleton<ITcpClientFactory, TcpClientFactory>();

            serviceCollection.AddScoped<INetworkServer, NetworkServer>();
            serviceCollection.AddScoped<INetworkClient, NetworkClient>();
            return serviceCollection;
        }
    }
}
