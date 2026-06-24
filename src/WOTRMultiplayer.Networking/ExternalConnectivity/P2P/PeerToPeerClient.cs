using LiteNetLib;
using LiteNetLib.Utils;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using WOTRMultiplayer.Networking.Abstractions.ExternalConnections;

namespace WOTRMultiplayer.Networking.ExternalConnectivity.P2P
{
    public class PeerToPeerClient : INetEventListener, INatPunchListener, IPeerToPeerClient
    {
        private readonly NetManager _net;
        private readonly NetDataWriter _writer = new();
        private readonly ILogger<PeerToPeerClient> _logger;
        private CancellationTokenSource _updateLoop;

        public bool IsActive => _net != null && _net.IsRunning;

        public int LocalPort => _net.LocalPort;

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

        public void Reset()
        {
            _net.Stop(true);
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

            _writer.Reset();
            _writer.Put($"Ping {Guid.NewGuid()}");
            peer.Send(_writer, DeliveryMethod.ReliableOrdered);
        }

        public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod method)
        {
            var message = reader.GetString();
            _logger.LogInformation("{Message}. PeerId={PeerId}", message, peer.Id);

            reader.Recycle();
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

            _net.Connect(targetEndPoint, "wotr");
        }

        public void OnNetworkError(IPEndPoint endPoint, SocketError socketError)
        {
        }

        public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
        {
        }

        public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
        {
        }

        public void OnConnectionRequest(ConnectionRequest request)
        {
            _logger.LogInformation("OnConnectionRequest. Endpoint={Endpoint}", request.RemoteEndPoint);
            request.AcceptIfKey("wotr");
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