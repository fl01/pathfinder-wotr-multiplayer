using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using WOTRMultiplayer.Networking.Abstractions;
using WOTRMultiplayer.Networking.Abstractions.ExternalConnections;
using WOTRMultiplayer.Networking.Consuming;
using WOTRMultiplayer.Networking.ExternalConnectivity;
using WOTRMultiplayer.Networking.ExternalConnectivity.P2P;
using WOTRMultiplayer.Networking.ExternalConnectivity.SignalR;
using WOTRMultiplayer.Networking.Messages;

namespace WOTRMultiplayer.Networking.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static ServiceCollection ConfigureNetworking(this ServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton<IIPEndPointParser, IPEndPointParser>();
            serviceCollection.AddSingleton<ITcpClientFactory, TcpClientFactory>();

            serviceCollection.AddSingleton<IExternalConnectionFactory, ExternalConnectionFactory>();
            serviceCollection.AddSingleton<IExternalMessageRegistry, ExternalMessageRegistry>();
            serviceCollection.AddSingleton<IPeerToPeerClient, PeerToPeerClient>();

            serviceCollection.AddSingleton<INetworkServer, NetworkServer>();
            serviceCollection.AddSingleton<INetworkClient, NetworkClient>();
            serviceCollection.AddTransient<IMessageConsumer, MessageConsumer>();

            NetworkMessages.Register(Assembly.GetExecutingAssembly());

            return serviceCollection;
        }
    }
}
