using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Networking.Abstractions.ExternalConnections;

namespace WOTRMultiplayer.Networking.ExternalConnectivity
{
    public class ExternalMessageRegistry : IExternalMessageRegistry
    {
        private readonly ILogger<ExternalMessageRegistry> _logger;
        private readonly Dictionary<MessageType, Dictionary<int, Type>> _messageTypeToVersionedType = [];
        private readonly Dictionary<Type, ExternalMessageAttribute> _typeToMessageMetadata = [];

        public ExternalMessageRegistry(ILogger<ExternalMessageRegistry> logger)
        {
            _logger = logger;
        }

        public ExternalMessageAttribute GetMessageMetadata(object message)
        {
            if (message == null)
            {
                _logger.LogError("Unable to get metadata for null message");
                return null;
            }

            var type = message.GetType();
            if (!_typeToMessageMetadata.TryGetValue(type, out var metadata))
            {
                _logger.LogError("Message is not registered correctly. Type={Type}", type);
                return null;
            }

            return metadata;
        }

        public void Register(Assembly[] assemblies)
        {
            foreach (var assembly in assemblies)
            {
                var messages = assembly.GetTypes().Where(x => x.GetCustomAttribute<ExternalMessageAttribute>() != null).ToList();
                foreach (var message in messages)
                {
                    var attribute = message.GetCustomAttribute<ExternalMessageAttribute>();
                    if (!_messageTypeToVersionedType.TryGetValue(attribute.MessageType, out var versioned))
                    {
                        versioned = [];
                    }

                    versioned[attribute.Version] = message;
                    _messageTypeToVersionedType[attribute.MessageType] = versioned;
                    _typeToMessageMetadata[message] = attribute;
                }
            }
        }

        public object Deserialize(MessageType messageType, int version, JsonElement data)
        {
            if (!_messageTypeToVersionedType.TryGetValue(messageType, out var versions))
            {
                _logger.LogError("Unregistered message. Type={Type}", messageType);
                return null;
            }

            if (!versions.TryGetValue(version, out var type))
            {
                _logger.LogError("Missing message version. Type={Type}, Version={Version}", messageType, version);
                return null;
            }

            var message = data.Deserialize(type);
            return message;
        }
    }
}
