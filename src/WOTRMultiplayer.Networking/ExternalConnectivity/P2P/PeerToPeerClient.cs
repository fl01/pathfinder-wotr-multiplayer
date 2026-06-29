using LiteNetLib;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using WOTRMultiplayer.Networking.Abstractions;
using WOTRMultiplayer.Networking.Abstractions.ExternalConnections;
using WOTRMultiplayer.Networking.Consuming;
using WOTRMultiplayer.Networking.Messages;

namespace WOTRMultiplayer.Networking.ExternalConnectivity.P2P
{
    public class PeerToPeerClient : INetEventListener, INatPunchListener, IPeerToPeerClient
    {
        private readonly NetManager _net;
        private readonly ILogger<PeerToPeerClient> _logger;
        private readonly ThreadLocal<MemoryStream> _senderStream = new(() => new MemoryStream(1024));
        private readonly IMessageConsumer _messageConsumer;

        private CancellationTokenSource _updateLoop;

        public const string P2PKey = "wotr";

        public bool IsActive => _net != null && _net.IsRunning;

        public int LocalPort => _net.LocalPort;

        public Action<int> OnNewPeerConnected { get; set; }

        public PeerToPeerClient(
            ILogger<PeerToPeerClient> logger,
            IMessageConsumer messageConsumer)
        {
            _logger = logger;
            _messageConsumer = messageConsumer;

            _net = new NetManager(this)
            {
                AutoRecycle = true,
                NatPunchEnabled = true
            };

            _net.NatPunchModule.Init(this);
        }

        public bool Start(int localPort)
        {
            if (_net.IsRunning)
            {
                return true;
            }

            var isStarted = _net.Start(localPort);
            if (!isStarted)
            {
                _logger.LogError("Unable to start LiteNetLib manager. Port={Port}", localPort);
                return false;
            }

            ResetUpdateLoop();
            _updateLoop = new CancellationTokenSource();
            Task.Run(() => UpdateLoopAsync(_updateLoop.Token));

            return true;
        }

        public void Send(object message)
        {
            try
            {
                var stream = _senderStream.Value;
                stream.Position = 0;
                stream.SetLength(0);

                var typeWriter = new BinaryWriter(stream);

                var type = message.GetType();
                var typeId = NetworkMessages.Get(type);
                if (typeId == null)
                {
                    _logger.LogError("Message is not registered correctly. Type={Type}", type);
                    return;
                }

                typeWriter.Write(typeId.Value);
                ProtoBuf.Meta.RuntimeTypeModel.Default.Serialize(stream, message);
                var data = stream.GetBuffer();
                var length = (int)stream.Length;
                var peers = _net.ConnectedPeerList.ToList();
                foreach (var peer in peers)
                {
                    try
                    {
                        peer.Send(data, 0, length, DeliveryMethod.ReliableOrdered);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Unable to send data to peer. PeerId={PeerId}", peer.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to send data. Type={Type}", message?.GetType().Name);
                throw;
            }
        }

        public void Reset()
        {
            _net.Stop();
            ResetUpdateLoop();
        }

        public void Introduce(string host, int port, string sessionId)
        {
            try
            {
                _logger.LogInformation("Sending NatIntroduce request. Host={Host}, Port={Port}, SessionId={SessionId}", host, port, sessionId);
                _net.NatPunchModule.SendNatIntroduceRequest(host, port, sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to introduce");
                throw;
            }
        }

        public void OnPeerConnected(NetPeer peer)
        {
            _logger.LogInformation("Peer has been connected. Address={Address}, Port={Port}", peer.Address, peer.Port);
            OnNewPeerConnected?.Invoke(peer.Id);
        }

        public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod method)
        {
            try
            {
                var rawType = reader.GetInt();
                var messageType = NetworkMessages.Get(rawType);
                if (messageType == null)
                {
                    _logger.LogError("Message type is not registered. Type={Type}", rawType);
                    return;
                }

                var data = reader.GetBytesSegment(reader.AvailableBytes);
                var message = ProtoBuf.Meta.RuntimeTypeModel.Default.Deserialize(data, messageType, null, null);
                var metadata = new NetworkMessageMetadata(0, message);
                _messageConsumer.Enqueue(metadata);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to deserialize p2p message");
            }
        }
        
        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            _logger.LogInformation("Peer has been disconnected. Address={Address}, Port={Port}, Reason={Reason}", peer.Address, peer.Port, disconnectInfo.Reason);
        }

        public void OnNatIntroductionRequest(IPEndPoint localEndPoint, IPEndPoint remoteEndPoint, string token)
        {
            _logger.LogInformation("NAT introduction request. LocalEndpoint={LocalEndpoint}, RemoteEndpoint={RemoteEndpoint}, Token={Token}", localEndPoint, remoteEndPoint, token);
        }

        public void OnNatIntroductionSuccess(IPEndPoint targetEndPoint, NatAddressType type, string token)
        {
            _logger.LogInformation("NAT introduction succeeded. Endpoint={Endpoint}, Type={Type}, Token={Token}", targetEndPoint, type, token);

            _net.Connect(targetEndPoint, P2PKey);
        }

        public void OnNetworkError(IPEndPoint endPoint, SocketError socketError)
        {
            _logger.LogError("Network error. Error={Error}", socketError);
        }

        public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
        {
            _logger.LogInformation("OnNetworkReceiveUnconnected. Type={Type}", messageType);
        }

        public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
        {
        }

        public void OnConnectionRequest(ConnectionRequest request)
        {
            _logger.LogInformation("OnConnectionRequest. Endpoint={Endpoint}", request.RemoteEndPoint);
            request.AcceptIfKey(P2PKey);
        }

        private void ResetUpdateLoop()
        {
            _updateLoop?.Cancel();
            _updateLoop = null;
        }

        private async Task UpdateLoopAsync(CancellationToken token)
        {
            _logger.LogInformation("Event-polling loop has been started");
            var delay = TimeSpan.FromMilliseconds(20);
            while (!token.IsCancellationRequested)
            {
                _net.PollEvents();
                _net.NatPunchModule.PollEvents();
                await Task.Delay(delay, token);
            }

            _logger.LogInformation("Event-polling loop has been ended");
        }
    }
}