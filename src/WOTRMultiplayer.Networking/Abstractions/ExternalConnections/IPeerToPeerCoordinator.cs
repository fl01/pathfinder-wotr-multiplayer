using System;
using System.Threading.Tasks;

namespace WOTRMultiplayer.Networking.Abstractions.ExternalConnections
{
    public interface IPeerToPeerCoordinator
    {
        Func<Task> OnReconnected { get; set; }

        Func<Task> OnReconnecting { get; set; }

        Task StopAsync(string code);

        Task ConnectAsync();

        IPeerToPeerCoordinator On<T>(Func<T, Task> handler)
            where T : class;

        Task SendAsync(object message);
    }
}
