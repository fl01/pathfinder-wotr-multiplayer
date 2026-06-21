using System;

namespace WOTRMultiplayer.Networking.ExternalConnectivity
{
    [AttributeUsage(AttributeTargets.Class)]
    public class ExternalMessageAttribute : Attribute
    {
        public int Version { get; private set; }

        public MessageType MessageType { get; private set; }

        public ExternalMessageAttribute(MessageType messageType, int version)
        {
            Version = version;
            MessageType = messageType;
        }
    }
}
