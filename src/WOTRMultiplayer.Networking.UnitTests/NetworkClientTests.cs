using System;
using System.Threading.Tasks;
using BeetleX.Clients;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using WOTRMultiplayer.Networking.Abstractions;
using WOTRMultiplayer.Networking.Consuming;

namespace WOTRMultiplayer.Networking.UnitTests
{
    [TestFixture]
    public class NetworkClientTests
    {
        private NetworkClient _client;

        private ILogger<NetworkClient> _logger;
        private ITcpClientFactory _tcpClientFactory;
        private IMessageConsumer _messageConsumer;

        [SetUp]
        public void SetUp()
        {
            _logger = A.Fake<ILogger<NetworkClient>>();
            _tcpClientFactory = A.Fake<ITcpClientFactory>();
            _messageConsumer = A.Fake<IMessageConsumer>();

            _client = new NetworkClient(_logger, _tcpClientFactory, _messageConsumer);
        }

        [Test]
        public async Task ConnectAsync_ValidHostAndPort_CallsFactory()
        {
            // Arrange
            var host = Guid.NewGuid().ToString();
            var port = new Random().Next(1, short.MaxValue);

            // Act
            await _client.ConnectAsync(host, port, TimeSpan.FromMinutes(1));

            // Assert
            A.CallTo(() => _tcpClientFactory.Create(host, port)).MustHaveHappenedOnceExactly();
        }

        [Test]
        public async Task ConnectAsync_ValidHostAndPort_ConnectingIsSetToTrue()
        {
            // Arrange
            var host = Guid.NewGuid().ToString();
            var port = new Random().Next(1, short.MaxValue);

            // Act
            await _client.ConnectAsync(host, port, TimeSpan.FromMinutes(1));

            // Assert
            Assert.That(_client.IsConnecting, Is.True);
        }

        [Test]
        public async Task ConnectAsync_ValidHostAndPort_ConfiguresClientAndCallsConnect()
        {
            // Arrange
            var host = Guid.NewGuid().ToString();
            var port = new Random().Next(1, short.MaxValue);
            var fakeClient = A.Fake<ITcpClient>();
            A.CallTo(() => _tcpClientFactory.Create(host, port)).Returns(fakeClient);

            // Act
            await _client.ConnectAsync(host, port, TimeSpan.FromMinutes(1));

            // Assert
            A.CallToSet(() => fakeClient.ClientError).MustHaveHappenedOnceExactly();
            A.CallToSet(() => fakeClient.Connected).MustHaveHappenedOnceExactly();
            A.CallToSet(() => fakeClient.PacketReceive).MustHaveHappenedOnceExactly();
            A.CallTo(() => fakeClient.Connect()).MustHaveHappenedOnceExactly();
        }

        [Test]
        public async Task OnClientConnected_Invoked_SetsIsConnectingToFalseAndCallsOnConnected()
        {
            // Arrange
            var host = Guid.NewGuid().ToString();
            var port = new Random().Next(1, short.MaxValue);
            var fakeTcpClient = A.Fake<ITcpClient>();
            A.CallTo(() => _tcpClientFactory.Create(host, port)).Returns(fakeTcpClient);
            await _client.ConnectAsync(host, port, TimeSpan.FromMinutes(1));
            var fakeClient = A.Fake<IClient>();
            var isOnConnectedInvoked = false;
            _client.OnConnected = _ => isOnConnectedInvoked = true;

            // Act
            fakeTcpClient.Connected.Invoke(fakeClient);

            // Assert
            Assert.That(_client.IsConnecting, Is.False);
            Assert.That(isOnConnectedInvoked, Is.True);
        }

        [Test]
        public async Task OnClientError_Invoked_SetsIsConnectingToFalseAndCallsOnError()
        {
            // Arrange
            var host = Guid.NewGuid().ToString();
            var port = new Random().Next(1, short.MaxValue);
            var fakeTcpClient = A.Fake<ITcpClient>();
            A.CallTo(() => _tcpClientFactory.Create(host, port)).Returns(fakeTcpClient);
            await _client.ConnectAsync(host, port, TimeSpan.FromMinutes(1));
            var fakeClient = A.Fake<IClient>();
            var isOnErrorInvoked = false;
            _client.OnError = _ => isOnErrorInvoked = true;
            var errorArgs = new ClientErrorArgs { };

            // Act
            fakeTcpClient.ClientError.Invoke(fakeClient, errorArgs);

            // Assert
            Assert.That(_client.IsConnecting, Is.False);
            Assert.That(isOnErrorInvoked, Is.True);
        }

        [Test]
        public async Task PacketReceive_HandlerIsConfigured_CallsHandler()
        {
            // Arrange
            var host = Guid.NewGuid().ToString();
            var port = new Random().Next(1, short.MaxValue);
            var fakeTcpClient = A.Fake<ITcpClient>();
            A.CallTo(() => _tcpClientFactory.Create(host, port)).Returns(fakeTcpClient);
            await _client.ConnectAsync(host, port, TimeSpan.FromMinutes(1));
            var fakeClient = A.Fake<IClient>();
            fakeClient.Token = new NetworkConnectionToken { Id = 1234 };
            var message = new object();

            // Act
            fakeTcpClient.PacketReceive.Invoke(fakeClient, message);

            // Assert
            A.CallTo(() => _messageConsumer.Enqueue(A<NetworkMessageMetadata>.That.Matches(x => x.Message == message))).MustHaveHappenedOnceExactly();
        }
    }
}
