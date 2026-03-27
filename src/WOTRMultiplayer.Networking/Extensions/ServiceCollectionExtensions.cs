using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using WOTRMultiplayer.Networking.Abstractions;
using WOTRMultiplayer.Networking.Consuming;
using WOTRMultiplayer.Networking.Messages;

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

            NetworkMessages.Register(Assembly.GetExecutingAssembly());

            return serviceCollection;
        }
    }
}
