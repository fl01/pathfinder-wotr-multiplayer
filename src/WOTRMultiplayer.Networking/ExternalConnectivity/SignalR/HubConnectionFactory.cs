using System;
using System.Net;
using System.Net.Http;
using Microsoft.AspNetCore.SignalR.Client;
using WOTRMultiplayer.Networking.Abstractions;

namespace WOTRMultiplayer.Networking.ExternalConnectivity.SignalR
{
    public class HubConnectionFactory : IHubConnectionFactory
    {
        public HubConnection Create(Uri url)
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
            return hub;
        }
    }
}
