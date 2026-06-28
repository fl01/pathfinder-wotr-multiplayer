using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Net;
using System.Net.Http;
using WOTRMultiplayer.Networking.Abstractions;
using WOTRMultiplayer.Networking.Abstractions.ExternalConnections;
using WOTRMultiplayer.Networking.ExternalConnectivity.P2P;

namespace WOTRMultiplayer.Networking.ExternalConnectivity.SignalR
{
    public class ExternalConnectionFactory : IExternalConnectionFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public ExternalConnectionFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public IExternalConnection Create(Uri url)
        {
            var hub = new HubConnectionBuilder()
                .WithUrl(url, options =>
                {
                    options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets;
                    options.HttpMessageHandlerFactory = _ =>
                    new HttpClientHandler
                    {
                        UseCookies = false,
                        UseProxy = false,
                        AutomaticDecompression = DecompressionMethods.None
                    };
                })
                .Build();

            var connection = ActivatorUtilities.CreateInstance<SignalRExternalConnection>(_serviceProvider, hub);
            return connection;
        }

        public IPeerToPeerClient CreateP2P(IMessageConsumer messageConsumer)
        {
            var p2p = ActivatorUtilities.CreateInstance<PeerToPeerClient>(_serviceProvider, messageConsumer);
            return p2p;
        }
    }
}
