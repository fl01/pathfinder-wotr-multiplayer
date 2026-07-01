using System.Threading.Tasks;
using BeetleX.Clients;
using WOTRMultiplayer.Networking.Abstractions.TCP;

namespace WOTRMultiplayer.Networking.Channels.TCP
{
    public class BeetleTcpClient : AsyncTcpClient, ITcpClient
    {
        Task ITcpClient.SendAsync(object message)
        {
            return Send(message);
        }
    }
}
