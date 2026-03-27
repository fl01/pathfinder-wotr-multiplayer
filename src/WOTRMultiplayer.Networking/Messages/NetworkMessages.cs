using System.Reflection;
using WOTRMultiplayer.Logging.Object;

namespace WOTRMultiplayer.Networking.Messages
{
    public static class NetworkMessages
    {
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
                }
            }
        }
    }
}
