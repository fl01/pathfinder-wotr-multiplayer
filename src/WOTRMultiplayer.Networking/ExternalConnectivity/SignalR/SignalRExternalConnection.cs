using System;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Networking.Abstractions.ExternalConnections;

namespace WOTRMultiplayer.Networking.ExternalConnectivity.SignalR
{
    public class SignalRExternalConnection : IExternalConnection
    {
        private readonly ILogger<SignalRExternalConnection> _logger;
        private readonly HubConnection _hub;
        private readonly IExternalMessageRegistry _messageRegistry;
        private readonly ConcurrentDictionary<Type, Func<object, Task>> _handlers = [];

        public const string DispatchMethodName = "Dispatch";
        public Func<Task> OnReconnected { get; set; }

        public SignalRExternalConnection(
            ILogger<SignalRExternalConnection> logger,
            IExternalMessageRegistry messageRegistry,
            HubConnection hub)
        {
            _logger = logger;
            _hub = hub;
            _messageRegistry = messageRegistry;

            _hub.On<MessageEnvelope>(DispatchMethodName, Dispatch);
            _hub.Reconnected += OnHubReconnected;
        }

        public IExternalConnection On<T>(Func<T, Task> handler)
            where T : class
        {
            _handlers.TryAdd(typeof(T), message => handler((T)message));
            return this;
        }

        public Task SendAsync(object message)
        {
            var messageMetadata = _messageRegistry.GetMessageMetadata(message);
            if (messageMetadata == null)
            {
                _logger.LogError("Missing message metadata. Type={Type}", message?.GetType().Name);
                return Task.CompletedTask;
            }

            var envelope = new MessageEnvelope
            {
                Type = messageMetadata.MessageType,
                Version = messageMetadata.Version,
                Data = JsonSerializer.SerializeToElement(message)
            };

            return _hub.SendAsync(DispatchMethodName, envelope);
        }

        public Task ConnectAsync(CancellationToken cancellationToken)
        {
            return _hub.StartAsync(cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _hub.Reconnected -= OnHubReconnected;
            return _hub.StopAsync(cancellationToken);
        }

        private Task OnHubReconnected(string connectionId)
        {
            _logger.LogWarning("Hub has been reconnected. ConnectionId={ConnectionId}");
            var handler = OnReconnected?.Invoke();
            return handler ?? Task.CompletedTask;
        }

        private async Task Dispatch(MessageEnvelope envelope)
        {
            try
            {
                var message = _messageRegistry.Deserialize(envelope.Type, envelope.Version, envelope.Data);
                if (message == null)
                {
                    return;
                }

                var type = message.GetType();
                if (!_handlers.TryGetValue(type, out var handler))
                {
                    _logger.LogWarning("Message handler is not registered. Message={Message}", type.Name);
                    return;
                }

                await handler.Invoke(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while handling message. Type={Type}", envelope?.Type);
                throw;
            }
        }
    }
}
