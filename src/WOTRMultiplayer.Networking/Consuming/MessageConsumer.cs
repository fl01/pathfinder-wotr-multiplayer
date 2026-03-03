using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Logging.Extensions;
using WOTRMultiplayer.Networking.Abstractions;
using WOTRMultiplayer.Networking.Messages.Lobby;

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
            _cancellation?.Cancel();
            _isRunning = false;
        }

        public void On<TMessage>(Action<long, TMessage> messageHandler, MessageHandlerPriority messageHandlerPriority)
            where TMessage : class
        {
            var handlers = _consumers.GetOrAdd(typeof(TMessage), []);
            var priority = messageHandlerPriority switch
            {
                MessageHandlerPriority.High => handlers.Keys.Count == 0 ? -1 : handlers.Keys.Min() - 1,
                _ or MessageHandlerPriority.Default => handlers.Keys.Count == 0 ? 0 : handlers.Keys.Max() + 1
            };
            handlers.Add(priority, (player, message) => messageHandler(player, (TMessage)message));
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
                        Task.Run(Consume, _cancellation.Token);
                    }
                }
            }
        }

        private void Consume()
        {
            _logger.LogInformation("Message consumer has been started");

            try
            {
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

                    if (metadata.Message is not NotifySaveGameChunkCreated and not NotifySaveGameTransferProgressChanged)
                    {
                        _logger.LogObject(LogLevel.Information, "Received {MessageType}. ReceivedFrom={ReceivedFrom}, Consumers={Consumers}", metadata.Message, metadata.PlayerId, configuredHandlers.Count);
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
                            _logger.LogError(ex, "Error while consuming message. PlayerId={PlayerId}, Type={Type}", metadata.PlayerId, messageType);
                        }
                    }
                }
            }
            catch (ThreadAbortException)
            {
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled consumer error");
                throw;
            }

            _logger.LogWarning("Message consumer has been terminated");
        }
    }
}
