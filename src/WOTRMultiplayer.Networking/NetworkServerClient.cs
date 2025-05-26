using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using BeetleX;
using BeetleX.Clients;

namespace WOTRMultiplayer.Networking
{
    public class NetworkServerClient
    {
        private AsyncTcpClient _client;
        private readonly ConcurrentDictionary<Type, Action<object>> _handlers = new();

        public async Task ConnectAsync(string host, int port)
        {
            _client = SocketFactory.CreateClient<AsyncTcpClient>(new Messages.ProtobufClientPacket(), host, port);
            _client.PacketReceive = OnPackedReceived;
            var status = await _client.Connect();
        }

        public NetworkServerClient Register<TMessage>(Action<TMessage> handler)
            where TMessage : class
        {
            _handlers.TryAdd(typeof(TMessage), message => handler((TMessage)message));

            return this;
        }

        public Task Send(object message)
        {
            return _client.Send(message);
        }

        private void OnPackedReceived(IClient client, object message)
        {
            if (!_handlers.TryGetValue(message.GetType(), out var handler))
            {
                return;
            }

            try
            {
                handler(message);
            }
            catch (Exception ex)
            {
            }
        }
    }
}
