using System;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Networking.Abstractions.ExternalConnections;
using WOTRMultiplayer.Networking.ExternalConnectivity.Messages;

namespace WOTRMultiplayer.Networking.ExternalConnectivity.SignalR
{
    public class SignalRCoordinator : IPeerToPeerCoordinator
    {
        private readonly ILogger<SignalRCoordinator> _logger;
        private readonly HubConnection _hub;
        private readonly IExternalMessageRegistry _messageRegistry;
        private readonly ConcurrentDictionary<Type, Func<object, Task>> _handlers = [];

        public const string DispatchMethodName = "Dispatch";

        public Func<Task> OnReconnected { get; set; }

        public Func<Task> OnReconnecting { get; set; }

        public SignalRCoordinator(
            ILogger<SignalRCoordinator> logger,
            IExternalMessageRegistry messageRegistry,
            HubConnection hub)
        {
            _logger = logger;
            _hub = hub;
            _messageRegistry = messageRegistry;

            _hub.On<MessageEnvelope>(DispatchMethodName, Dispatch);
            _hub.Reconnected += OnHubReconnected;
            _hub.Reconnecting += OnHubReconnecting;
        }

        public IPeerToPeerCoordinator On<T>(Func<T, Task> handler)
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

            _logger.LogInformation("Sending message. Type={Type}, Version={Version}", envelope.Type, envelope.Version);

            return _hub.SendAsync(DispatchMethodName, envelope);
        }

        public Task ConnectAsync()
        {
            return _hub.StartAsync();
        }

        public async Task StopAsync(string code)
        {
            _hub.Reconnected -= OnHubReconnected;
            _hub.Reconnecting -= OnHubReconnecting;

            if (_hub.State == HubConnectionState.Connected && !string.IsNullOrEmpty(code))
            {
                var terminateGame = new TerminateGameMessage
                {
                    Code = code
                };
                await SendAsync(terminateGame);
            }

            await _hub.StopAsync();
        }

        private Task OnHubReconnecting(Exception exception)
        {
            _logger.LogWarning(exception, "Reconnecting");
            var handler = OnReconnecting?.Invoke();
            return handler ?? Task.CompletedTask;
        }

        private Task OnHubReconnected(string connectionId)
        {
            _logger.LogWarning("Reconnected. ConnectionId={ConnectionId}", connectionId);
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
                    _logger.LogWarning("Message handler is not registered. Type={Type}", type.Name);
                    return;
                }

                _logger.LogInformation("Received {MessageType}. Version={Version}", type.Name, envelope.Version);

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
