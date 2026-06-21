using System;

namespace WOTRMultiplayer.Networking.Abstractions.ExternalConnections
{
    public interface IExternalConnectionFactory
    {
        IExternalConnection Create(Uri url);
    }
}
