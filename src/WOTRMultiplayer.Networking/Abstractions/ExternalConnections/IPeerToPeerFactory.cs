using System;

namespace WOTRMultiplayer.Networking.Abstractions.ExternalConnections
{
    public interface IPeerToPeerFactory
    {
        IPeerToPeerCoordinator Create(Uri url);
    }
}
