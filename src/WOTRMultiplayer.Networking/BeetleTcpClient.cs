using System.Threading.Tasks;
using BeetleX.Clients;
using WOTRMultiplayer.Networking.Abstractions;

namespace WOTRMultiplayer.Networking
{
    public class BeetleTcpClient : AsyncTcpClient, ITcpClient
    {
        Task ITcpClient.SendAsync(object message)
        {
            return Send(message);
        }
    }
}
