using Microsoft.Extensions.DependencyInjection;
using WOTRMultiplayer.Networking.Abstractions;
using WOTRMultiplayer.Networking.Consuming;

namespace WOTRMultiplayer.Networking.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static ServiceCollection ConfigureNetworking(this ServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton<IIPEndPointParser, IPEndPointParser>();
            serviceCollection.AddSingleton<ITcpClientFactory, TcpClientFactory>();

            serviceCollection.AddSingleton<INetworkServer, NetworkServer>();
            serviceCollection.AddSingleton<INetworkClient, NetworkClient>();
            serviceCollection.AddTransient<IMessageConsumer, MessageConsumer>();
            return serviceCollection;
        }
    }
}
