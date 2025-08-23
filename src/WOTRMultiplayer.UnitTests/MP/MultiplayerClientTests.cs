using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using AutoMapper;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.IO;
using WOTRMultiplayer.Abstractions.MP;
using WOTRMultiplayer.Abstractions.Random;
using WOTRMultiplayer.Config.Mapping;
using WOTRMultiplayer.MP.Actors;
using WOTRMultiplayer.MP.Entities;
using WOTRMultiplayer.MP.Entities.Rolls.Claiming.Values;
using WOTRMultiplayer.Networking.Abstractions;
using WOTRMultiplayer.Networking.Messages.Game;
using WOTRMultiplayer.Networking.Messages.Requests;
using WOTRMultiplayer.UnitTests.FakeRules;

namespace WOTRMultiplayer.UnitTests.MP
{
    [TestFixture]
    public class MultiplayerClientTests
    {
        private MultiplayerClient _multiplayerClient;

        private ILogger<MultiplayerClient> _logger;
        private IGameInteractionService _gameInteractionService;
        private IMultiplayerSettingsProvider _multiplayerSettingsProvider;
        private IIPEndPointParser _endpointParser;
        private IFileSystemService _fileSystemService;
        private INetworkClient _networkClient;
        private IDiceRollStorage _diceRollStorage;
        private IValueGenerator _valueGenerator;
        private IMapper _mapper;

        [SetUp]
        public void SetUp()
        {
            _mapper = new MapperConfiguration(x =>
            {
                x.AddProfile<NetworkMessagesProfile>();
            }).CreateMapper();

            _logger = A.Fake<ILogger<MultiplayerClient>>();
            _gameInteractionService = A.Fake<IGameInteractionService>();
            _endpointParser = A.Fake<IIPEndPointParser>();
            _multiplayerSettingsProvider = A.Fake<IMultiplayerSettingsProvider>();
            _fileSystemService = A.Fake<IFileSystemService>();

            _networkClient = A.Fake<INetworkClient>();
            Fake.GetFakeManager(_networkClient).AddRuleFirst(new NetworkReceiverFakeRule());

            _diceRollStorage = A.Fake<IDiceRollStorage>();
            _valueGenerator = A.Fake<IValueGenerator>();

            _multiplayerClient = new MultiplayerClient(
                _logger,
                _gameInteractionService,
                _endpointParser,
                _multiplayerSettingsProvider,
                _fileSystemService,
                _networkClient,
                _diceRollStorage,
                _valueGenerator,
                _mapper);
        }

        [Test]
        public void Connect_AddressIsNotParsed_ReturnsNotOkResult()
        {
            // Arrange
            var address = Guid.NewGuid().ToString();
            A.CallTo(() => _endpointParser.Parse(address)).Returns(null);

            // Act
            var result = _multiplayerClient.Connect(address);

            // Assert
            Assert.That(result.IsOk, Is.False);
        }

        [Test]
        public void Connect_InvalidPort_ReturnsNotOkResult()
        {
            // Arrange
            IPAddress.TryParse("192.168.1.1", out var parsedAddress);
            var endpoint = new IPEndPoint(parsedAddress, 0);
            var address = Guid.NewGuid().ToString();
            A.CallTo(() => _endpointParser.Parse(address)).Returns(endpoint);

            // Act
            var result = _multiplayerClient.Connect(address);

            // Assert
            Assert.That(result.IsOk, Is.False);
        }

        [Test]
        public void Connect_ValidAddress_RegistersHandlersAndCallsConnectOnNetworkClient()
        {
            // Arrange
            var parsedHost = "192.168.1.1";
            var parsedPort = 555;
            IPAddress.TryParse(parsedHost, out var parsedAddress);
            var endpoint = new IPEndPoint(parsedAddress, parsedPort);
            var address = Guid.NewGuid().ToString();
            A.CallTo(() => _endpointParser.Parse(address)).Returns(endpoint);

            // Act
            var result = _multiplayerClient.Connect(address);

            // Assert
            Assert.That(result.IsOk, Is.True);
            A.CallTo(() => _networkClient.On(A<Action<long, DiceRollValueRequest>>.Ignored)).MustHaveHappenedOnceExactly();
            A.CallTo(() => _networkClient.ConnectAsync(parsedHost, parsedPort)).MustHaveHappenedOnceExactly();
        }

        [Test]
        public async Task OnDiceRollValueRequest_PlayerIdIsSet_CallsDiceRollStorageAndSendsResult()
        {
            // Arrange
            var parsedHost = "192.168.1.1";
            var parsedPort = 555;
            IPAddress.TryParse(parsedHost, out var parsedAddress);
            var endpoint = new IPEndPoint(parsedAddress, parsedPort);
            var address = Guid.NewGuid().ToString();
            A.CallTo(() => _endpointParser.Parse(address)).Returns(endpoint);
            _multiplayerClient.Connect(address);
            var handler = FakeUtils.GetNetworkReceiverHandler<DiceRollValueRequest>(_networkClient);
            var request = new DiceRollValueRequest { RollId = 1, Timeout = TimeSpan.FromDays(1), UnitId = Guid.NewGuid().ToString(), PlayerId = 1 };
            var getRollTask = Task.FromResult<RollValueBase>(new NetworkIntRollValue());
            A.CallTo(() => _diceRollStorage.GetAsync<RollValueBase>(request.RollId, request.PlayerId.Value, request.Timeout)).Returns(getRollTask);

            // Act
            handler.Invoke(1, request);
            await getRollTask;

            // Assert
            A.CallTo(() => _diceRollStorage.GetAsync<RollValueBase>(request.RollId, request.PlayerId.Value, request.Timeout)).MustHaveHappened();
            A.CallTo(() => _networkClient.Send(A<DiceRollValueResponse>.Ignored)).MustHaveHappened();
        }

        [Test]
        public async Task OnDiceRollValueRequest_PlayerIdIsNotSet_CallsDiceRollStorageForHostIdAndSendsResult()
        {
            // Arrange
            var parsedHost = "192.168.1.1";
            var parsedPort = 555;
            IPAddress.TryParse(parsedHost, out var parsedAddress);
            var endpoint = new IPEndPoint(parsedAddress, parsedPort);
            var address = Guid.NewGuid().ToString();
            A.CallTo(() => _endpointParser.Parse(address)).Returns(endpoint);
            _multiplayerClient.Connect(address);
            var handler = FakeUtils.GetNetworkReceiverHandler<DiceRollValueRequest>(_networkClient);
            var request = new DiceRollValueRequest { RollId = 1, Timeout = TimeSpan.FromDays(1), UnitId = Guid.NewGuid().ToString(), PlayerId = null };
            var getRollTask = Task.FromResult<RollValueBase>(new NetworkIntRollValue());
            A.CallTo(() => _diceRollStorage.GetAsync<RollValueBase>(request.RollId, MultiplayerActorBase.LocalHostPlayerId, request.Timeout)).Returns(getRollTask);

            // Act
            handler.Invoke(1, request);
            await getRollTask;

            // Assert
            A.CallTo(() => _diceRollStorage.GetAsync<RollValueBase>(request.RollId, MultiplayerActorBase.LocalHostPlayerId, request.Timeout)).MustHaveHappened();
            A.CallTo(() => _networkClient.Send(A<DiceRollValueResponse>.Ignored)).MustHaveHappened();
        }

        [Test]
        public void OnNotifyUnitClicked_CallsGameInteractionService()
        {
            // Arrange
            var parsedHost = "192.168.1.1";
            var parsedPort = 555;
            IPAddress.TryParse(parsedHost, out var parsedAddress);
            var endpoint = new IPEndPoint(parsedAddress, parsedPort);
            var address = Guid.NewGuid().ToString();
            A.CallTo(() => _endpointParser.Parse(address)).Returns(endpoint);
            _multiplayerClient.Connect(address);
            var handler = FakeUtils.GetNetworkReceiverHandler<NotifyUnitClicked>(_networkClient);
            var request = new NotifyUnitClicked { Click = new Networking.Messages.Contracts.NetworkClick { } };

            // Act
            handler.Invoke(1, request);

            // Assert
            A.CallTo(() => _gameInteractionService.ClickUnit(A<NetworkClick>.Ignored)).MustHaveHappened();
        }
    }
}
