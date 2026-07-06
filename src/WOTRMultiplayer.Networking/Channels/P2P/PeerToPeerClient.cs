using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using LiteNetLib;
using Microsoft.Extensions.Logging;
using WOTRMultiplayer.Networking.Abstractions.ExternalConnections;
using WOTRMultiplayer.Networking.Consuming;
using WOTRMultiplayer.Networking.Messages;

namespace WOTRMultiplayer.Networking.Channels.P2P
{
    public class PeerToPeerClient : INetEventListener, INatPunchListener, IPeerToPeerClient
    {
        private readonly NetManager _net;
        private readonly ILogger<PeerToPeerClient> _logger;
        private readonly ThreadLocal<MemoryStream> _senderStream = new(() => new MemoryStream(1024));

        private CancellationTokenSource _updateLoop;
        public const string P2PKey = "WOTR";

        public bool IsActive => _net != null && _net.IsRunning;

        public int LocalPort => _net.LocalPort;

        public Action<int> OnPeerConnectedEvent { get; set; }

        public Action<int> OnPeerDisconnectedEvent { get; set; }

        public Action<NetworkMessageMetadata> OnMessageReceived { get; set; }

        public PeerToPeerClient(ILogger<PeerToPeerClient> logger)
        {
            _logger = logger;

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

        public void Send(long clientId, object message)
        {
            var peer = _net.ConnectedPeerList.FirstOrDefault(p => p.Id == clientId);
            if (peer == null)
            {
                _logger.LogError("Unable to find specified peer in connected peer list. Id={Id}", clientId);
                return;
            }

            Broadcast(message, [peer]);
        }

        public void BroadcastExcept(long clientId, object message)
        {
            var peers = _net.ConnectedPeerList.Where(p => p.Id != clientId).ToList();
            if (peers.Count == 0)
            {
                return;
            }

            Broadcast(message, peers);
        }

        public void Broadcast(object message)
        {
            var peers = _net.ConnectedPeerList.ToList();
            if (peers.Count == 0)
            {
                return;
            }

            Broadcast(message, peers);
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
            _logger.LogInformation("Peer has been connected. Id={Id}", peer.Id);
            OnPeerConnectedEvent?.Invoke(peer.Id);
        }

        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            _logger.LogInformation("Peer has been disconnected. Id={Id}, Reason={Reason}", peer.Id, disconnectInfo.Reason);
            OnPeerDisconnectedEvent?.Invoke(peer.Id);
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
                var metadata = new NetworkMessageMetadata(NetworkChannelType.P2P, peer.Id, message);
                OnMessageReceived?.Invoke(metadata);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to deserialize p2p message");
            }
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

        private void Broadcast(object message, List<NetPeer> peers)
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
    }
}