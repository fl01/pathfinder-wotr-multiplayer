using System;
using System.Threading;
using System.Threading.Tasks;

namespace WOTRMultiplayer.Networking.Abstractions.ExternalConnections
{
    public interface IExternalConnection
    {
        Func<Task> OnReconnected { get; set; }

        Task StopAsync(CancellationToken cancellationToken);

        Task ConnectAsync(CancellationToken cancellationToken);

        IExternalConnection On<T>(Func<T, Task> handler)
            where T : class;

        Task SendAsync(object message);
    }
}
