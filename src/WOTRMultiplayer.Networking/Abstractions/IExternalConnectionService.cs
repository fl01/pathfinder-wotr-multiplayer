using System;
using System.Threading.Tasks;
using WOTRMultiplayer.Networking.Configuration;

namespace WOTRMultiplayer.Networking.Abstractions
{
    public interface IExternalConnectionService
    {
        Action<string> OnGameCodeChanged { get; set; }

        Action OnConnected { get; set; }

        Action OnError { get; set; }

        Task ConnectAsync(ExternalServerConfiguration externalServer);

        void Reset();
    }
}
