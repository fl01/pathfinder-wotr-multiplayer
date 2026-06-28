using System;
using System.Collections.Generic;
using System.Reflection;
using WOTRMultiplayer.Logging.Object;

namespace WOTRMultiplayer.Networking.Messages
{
    public static class NetworkMessages
    {
        private static readonly Dictionary<int, Type> _idToType = [];
        private static readonly Dictionary<Type, int> _typeToId = [];

        public static Type Get(int id)
        {
            _idToType.TryGetValue(id, out var type);
            return type;
        }

        public static int? Get(Type type)
        {
            if (!_typeToId.TryGetValue(type, out var id))
            {
                return null;
            }

            return id;
        }

        public static void Register(params Assembly[] assemblies)
        {
            foreach (Assembly assembly in assemblies)
            {
                var types = assembly.GetTypes();
                foreach (var type in types)
                {
                    var messageTypeAttribute = type.GetTypeInfo().GetCustomAttribute<MessageTypeAttribute>();
                    if (messageTypeAttribute == null)
                    {
                        continue;
                    }

                    ObjectLoggingMetadata.Initialize(type);
                    BeetleXMessageTypes.MessageTypes.Register(type, messageTypeAttribute.Id);
                    _idToType[messageTypeAttribute.Id] = type;
                    _typeToId[type] = messageTypeAttribute.Id;
                }
            }
        }
    }
}
