using System;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Logging.Extensions;
using WOTRMultiplayer.Networking.Abstractions;
using WOTRMultiplayer.Networking.Abstractions.ExternalConnections;
using WOTRMultiplayer.Networking.Consuming;

namespace WOTRMultiplayer.Networking
{
    public abstract class NetworkConnectionBase : INetworkConnection
    {
        private readonly INetworkChannel _tcpNetworkChannel;
        private readonly IMessageConsumer _messageConsumer;

        protected ILogger Logger { get; private set; }

        protected IExternalConnectionService ExternalConnectionService { get; private set; }

        public bool IsActive => _tcpNetworkChannel.IsActive || ExternalConnectionService.IsActive;

        public NetworkConnectionBase(
            ILogger logger,
            IMessageConsumer messageConsumer,
            INetworkChannel tcpNetworkChannel,
            IExternalConnectionService externalConnectionService)
        {
            Logger = logger;
            _tcpNetworkChannel = tcpNetworkChannel;
            _messageConsumer = messageConsumer;
            ExternalConnectionService = externalConnectionService;

            _tcpNetworkChannel.OnMessageReceived = OnMessageReceived;
            ExternalConnectionService.OnMessageReceived = OnMessageReceived;
        }

        public virtual void Reset()
        {
            Logger.LogInformation("Reset");

            _messageConsumer.Reset();
            ExternalConnectionService.Reset();
        }

        public INetworkConnection On<TMessage>(Action<long, TMessage> messageHandler, MessageHandlerPriority priority = MessageHandlerPriority.Default)
            where TMessage : class
        {
            _messageConsumer.On<TMessage>(messageHandler, priority);
            return this;
        }

        public void Broadcast(object message)
        {
            Logger.LogObject(LogLevel.Information, "Sending {MessageType}.", message);

            if (_tcpNetworkChannel.IsActive)
            {
                _tcpNetworkChannel.Broadcast(message);
            }

            if (ExternalConnectionService.IsActive)
            {
                ExternalConnectionService.Broadcast(message);
            }
        }

        protected virtual void OnMessageReceived(NetworkMessageMetadata networkMessageMetadata)
        {
            _messageConsumer.Enqueue(networkMessageMetadata);
        }
    }
}
