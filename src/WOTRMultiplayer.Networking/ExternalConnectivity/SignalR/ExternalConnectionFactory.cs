using System;
using System.Net;
using System.Net.Http;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using WOTRMultiplayer.Networking.Abstractions.ExternalConnections;

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
    }
}
