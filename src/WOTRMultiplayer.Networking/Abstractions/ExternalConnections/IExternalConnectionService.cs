using System;
using System.Threading.Tasks;
using WOTRMultiplayer.Networking.Configuration;

namespace WOTRMultiplayer.Networking.Abstractions.ExternalConnections
{
    public interface IExternalConnectionService
    {
        Action<string> OnGameCodeChanged { get; set; }

        Action OnConnected { get; set; }

        Action OnError { get; set; }

        Task ConnectAsync(ExternalServerConfiguration externalServerConfiguration);

        void Reset();

        Task JoinGameAsync(string code, string password, int port);

        bool IsActive { get; }
    }
}
