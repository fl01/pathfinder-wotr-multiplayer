using System;
using System.Net;
using System.Net.Http;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using WOTRMultiplayer.Networking.Abstractions.ExternalConnections;
using WOTRMultiplayer.Networking.ExternalConnectivity.Retry;
using WOTRMultiplayer.Networking.ExternalConnectivity.SignalR;

namespace WOTRMultiplayer.Networking.ExternalConnectivity
{
    public class PeerToPeerFactory : IPeerToPeerFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public PeerToPeerFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public IPeerToPeerCoordinator Create(Uri url)
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
                .WithAutomaticReconnect(new FiniteTriesRetryPolicy(retryCount: 30, delay: TimeSpan.FromSeconds(3)))
                .Build();

            var connection = ActivatorUtilities.CreateInstance<SignalRCoordinator>(_serviceProvider, hub);
            return connection;
        }
    }
}
