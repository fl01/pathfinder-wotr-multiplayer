namespace WOTRMultiplayer.Networking.Abstractions
{
    public interface ITcpClientFactory
    {
        ITcpClient Create(string host, int port);
    }
}
