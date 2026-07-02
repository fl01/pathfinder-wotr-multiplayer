using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using WOTRMultiplayer.Networking.Abstractions;
using WOTRMultiplayer.Networking.Abstractions.ExternalConnections;
using WOTRMultiplayer.Networking.Abstractions.TCP;
using WOTRMultiplayer.Networking.Channels.P2P;
using WOTRMultiplayer.Networking.Channels.TCP;
using WOTRMultiplayer.Networking.Consuming;
using WOTRMultiplayer.Networking.ExternalConnectivity;
using WOTRMultiplayer.Networking.Messages;

namespace WOTRMultiplayer.Networking.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static ServiceCollection ConfigureNetworking(this ServiceCollection serviceCollection)
        {
            serviceCollection.AddTransient<IMessageConsumer, MessageConsumer>();

            serviceCollection.AddSingleton<INetworkHostConnection, NetworkHostConnection>();
            serviceCollection.AddSingleton<INetworkClientConnection, NetworkClientConnection>();

            serviceCollection.AddTransient<IExternalConnectionService, ExternalConnectionService>();
            serviceCollection.AddSingleton<IExternalMessageRegistry, ExternalMessageRegistry>();
            serviceCollection.AddSingleton<IPeerToPeerFactory, PeerToPeerFactory>();
            serviceCollection.AddSingleton<IPeerToPeerClient, PeerToPeerClient>();

            serviceCollection.AddSingleton<IIPEndPointParser, IPEndPointParser>();

            serviceCollection.AddSingleton<ITcpFactory, TcpFactory>();
            serviceCollection.AddSingleton<INetworkServer, NetworkServer>();
            serviceCollection.AddSingleton<INetworkClient, NetworkClient>();

            NetworkMessages.Register(Assembly.GetExecutingAssembly());

            return serviceCollection;
        }
    }
}
