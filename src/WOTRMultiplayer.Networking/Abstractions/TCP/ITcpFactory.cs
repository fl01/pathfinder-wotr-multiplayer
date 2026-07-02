using BeetleX;

namespace WOTRMultiplayer.Networking.Abstractions.TCP
{
    public interface ITcpFactory
    {
        ITcpClient CreateClient(string host, int port);

        ServerBuilder<TApp, TToken, TPacket> CreateServerBuilder<TApp, TToken, TPacket>()
            where TApp : IApplication, new()
            where TToken : ISessionToken, new()
            where TPacket : IPacket, new();
    }
}
