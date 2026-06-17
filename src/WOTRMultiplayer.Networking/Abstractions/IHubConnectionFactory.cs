using System;
using Microsoft.AspNetCore.SignalR.Client;

namespace WOTRMultiplayer.Networking.Abstractions
{
    public interface IHubConnectionFactory
    {
        HubConnection Create(Uri url);
    }
}
