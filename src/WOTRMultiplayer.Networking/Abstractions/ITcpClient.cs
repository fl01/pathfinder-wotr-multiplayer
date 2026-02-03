using System;
using System.Threading.Tasks;
using BeetleX.Clients;

namespace WOTRMultiplayer.Networking.Abstractions
{
    public interface ITcpClient : IDisposable
    {
        bool IsConnected { get; }

        EventClientError ClientError { get; set; }

        EventClientPacketCompleted PacketReceive { get; set; }

        EventClientConnected Connected { get; set; }

        Task SendAsync(object message);

        Task<ConnectStatus> Connect();
    }
}
