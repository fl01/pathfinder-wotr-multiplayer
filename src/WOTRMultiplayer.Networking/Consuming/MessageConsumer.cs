using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Networking.Abstractions;

namespace WOTRMultiplayer.Networking.Consuming
{
    public class MessageConsumer : IMessageConsumer
    {
        private readonly ConcurrentDictionary<Type, SortedList<int, Action<long, object>>> _consumers = [];
        private readonly BlockingCollection<NetworkMessageMetadata> _messages = [];
        private readonly object _actionLock = new();
        private readonly ILogger<MessageConsumer> _logger;

        private CancellationTokenSource _cancellation;
        private bool _isRunning = false;

        public MessageConsumer(ILogger<MessageConsumer> logger)
        {
            _logger = logger;
        }

        public void Reset()
        {
            _cancellation?.Dispose();
            _isRunning = false;
        }

        public void On<TMessage>(Action<long, TMessage> messageHandler, MessageHandlerPriority priority)
            where TMessage : class
        {
            switch (priority)
            {
                case MessageHandlerPriority.Default:
                    _consumers.AddOrUpdate(
                        typeof(TMessage),
                        k => new SortedList<int, Action<long, object>>
                        {
                            { default, (player, message) => messageHandler(player, (TMessage)message) }
                        },
                        (k, existing) =>
                        {
                            var next = existing.Keys.Max() + 1;
                            existing.Add(next, (player, message) => messageHandler(player, (TMessage)message));
                            return existing;
                        });
                    break;
            }
        }

        public void Enqueue(NetworkMessageMetadata message)
        {
            _messages.Add(message);

            if (!_isRunning)
            {
                lock (_actionLock)
                {
                    if (!_isRunning)
                    {
                        _isRunning = true;
                        _cancellation?.Dispose();
                        _cancellation = new CancellationTokenSource();
                        Task.Factory.StartNew(_ => Consume(), TaskCreationOptions.LongRunning, _cancellation.Token);
                    }
                }
            }
        }

        private void Consume()
        {
            _logger.LogInformation("Message consumer has been started");

            foreach (var metadata in _messages.GetConsumingEnumerable(_cancellation.Token))
            {
                if (_cancellation.Token.IsCancellationRequested)
                {
                    continue;
                }

                var messageType = metadata.Message.GetType();
                if (!_consumers.TryGetValue(messageType, out var configuredHandlers))
                {
                    _logger.LogError("There are no configured message handlers. Type={Type}", messageType);
                    continue;
                }

                var handlers = configuredHandlers.ToList();
                foreach (var handler in handlers)
                {
                    try
                    {
                        handler.Value.Invoke(metadata.PlayerId, metadata.Message);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error consuming message. PlayerId={PlayerId}, Type={Type}", metadata.PlayerId, messageType);
                    }
                }
            }

            _logger.LogInformation("Message consumer has been terminated");
        }
    }
}
