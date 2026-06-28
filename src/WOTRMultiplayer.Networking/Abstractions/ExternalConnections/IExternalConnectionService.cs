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

        Action<int> OnNewExternalConnection { get; set; }

        Task ConnectAsync(ExternalServerConfiguration externalServerConfiguration, IMessageConsumer messageConsumer);

        void Reset();

        Task JoinGameAsync(string code, string password);

        bool IsActive { get; }

        void Send(object data);

        void Send(long clientId, object message);

        void SendAllExcept(long clientId, object message);
    }
}
