namespace WOTRMultiplayer.Networking.Abstractions.TCP
{
    public interface ITcpClientFactory
    {
        ITcpClient Create(string host, int port);
    }
}
