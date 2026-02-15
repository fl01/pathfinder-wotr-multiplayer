using System;
using System.Net;
using System.Threading.Tasks;
using AutoMapper;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using WOTRMultiplayer.Abstractions;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.IO;
using WOTRMultiplayer.Abstractions.QueuedActions;
using WOTRMultiplayer.Abstractions.Random;
using WOTRMultiplayer.Abstractions.Settings;
using WOTRMultiplayer.Config.Mapping;
using WOTRMultiplayer.Entities;
using WOTRMultiplayer.Entities.Combat;
using WOTRMultiplayer.Entities.Rolls.Claiming.Values;
using WOTRMultiplayer.Networking.Abstractions;
using WOTRMultiplayer.Networking.Messages.Game;
using WOTRMultiplayer.Networking.Messages.Lobby;
using WOTRMultiplayer.Networking.Messages.Requests;
using WOTRMultiplayer.Services;
using WOTRMultiplayer.UnitTests.FakeRules;

namespace WOTRMultiplayer.UnitTests.Services
{
    [TestFixture]
    public class MultiplayerClientTests
    {
        private MultiplayerClient _multiplayerClient;

        private ILogger<MultiplayerClient> _logger;
        private IGameInteractionService _gameInteractionService;
        private ILevelingInteractionService _levelingInteractionService;
        private IPlayerNotificationService _playerNotificationService;
        private IDialogInteractionService _dialogInteractionService;
        private IGlobalMapInteractionService _globalMapInteractionService;
        private IPingInteractionService _pingInteractionService;
        private ICombatInteractionService _combatInteractionService;
        private IMultiplayerSettingsService _multiplayerSettingsProvider;
        private IIPEndPointParser _endpointParser;
        private IFileSystemService _fileSystemService;
        private IQueuedActionsRunner _actionsRunner;
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
            _levelingInteractionService = A.Fake<ILevelingInteractionService>();
            _playerNotificationService = A.Fake<IPlayerNotificationService>();
            _dialogInteractionService = A.Fake<IDialogInteractionService>();
            _globalMapInteractionService = A.Fake<IGlobalMapInteractionService>();
            _pingInteractionService = A.Fake<IPingInteractionService>();
            _combatInteractionService = A.Fake<ICombatInteractionService>();
            _endpointParser = A.Fake<IIPEndPointParser>();
            _multiplayerSettingsProvider = A.Fake<IMultiplayerSettingsService>();
            _fileSystemService = A.Fake<IFileSystemService>();
            _actionsRunner = A.Fake<IQueuedActionsRunner>();


            _networkClient = A.Fake<INetworkClient>();
            Fake.GetFakeManager(_networkClient).AddRuleFirst(new NetworkReceiverFakeRule());

            _diceRollStorage = A.Fake<IDiceRollStorage>();
            _valueGenerator = A.Fake<IValueGenerator>();

            _multiplayerClient = new MultiplayerClient(
                _logger,
                _gameInteractionService,
                _levelingInteractionService,
                _playerNotificationService,
                _dialogInteractionService,
                _globalMapInteractionService,
                _pingInteractionService,
                _combatInteractionService,
                _endpointParser,
                _multiplayerSettingsProvider,
                _fileSystemService,
                _networkClient,
                _diceRollStorage,
                _valueGenerator,
                _actionsRunner,
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
            A.CallTo(() => _networkClient.ConnectAsync(parsedHost, parsedPort, A<TimeSpan>.Ignored)).MustHaveHappenedOnceExactly();
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
            A.CallTo(() => _diceRollStorage.GetAsync<RollValueBase>(request.RollId, request.PlayerId, request.Timeout)).Returns(getRollTask);

            // Act
            handler.Invoke(1, request);
            await getRollTask;

            // Assert
            A.CallTo(() => _diceRollStorage.GetAsync<RollValueBase>(request.RollId, request.PlayerId, request.Timeout)).MustHaveHappened();
            A.CallTo(() => _networkClient.Send(A<DiceRollValueResponse>.Ignored)).MustHaveHappened();
        }

        [Test]
        public void OnNotifyUnitClicked_InCombatAndCannotGetUp_DoesNotCallGameInteractionService()
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
            _multiplayerClient.Game = new NetworkGame(new NetworkGameStartUp("hehe")) { Combat = new NetworkCombat() };
            A.CallTo(() => _combatInteractionService.CanRiderGetUp()).Returns(false);

            // Act
            handler.Invoke(1, request);

            // Assert
            A.CallTo(() => _gameInteractionService.ClickUnit(A<NetworkClick>.Ignored)).MustNotHaveHappened();
        }

        [Test]
        public void OnNotifyUnitClicked_InCombatAndCanGetUp_CallsGameInteractionService()
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
            _multiplayerClient.Game = new NetworkGame(new NetworkGameStartUp("hehe")) { Combat = new NetworkCombat() };
            A.CallTo(() => _combatInteractionService.CanRiderGetUp()).Returns(true);

            // Act
            handler.Invoke(1, request);

            // Assert
            A.CallTo(() => _gameInteractionService.ClickUnit(A<NetworkClick>.Ignored)).MustHaveHappened();
        }

        [Test]
        public void OnNotifyUnitClicked_NotInCombat_CallsGameInteractionService()
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
            _multiplayerClient.Game = new NetworkGame(new NetworkGameStartUp("hehe")) { Combat = null };

            // Act
            handler.Invoke(1, request);

            // Assert
            A.CallTo(() => _gameInteractionService.ClickUnit(A<NetworkClick>.Ignored)).MustHaveHappened();
        }

        [Test]
        public void OnNotifyLobbySaveGameChanged_StoresSaveFileAndSendsConfirmation()
        {
            // Arrange
            var parsedHost = "192.168.1.1";
            var parsedPort = 555;
            IPAddress.TryParse(parsedHost, out var parsedAddress);
            var endpoint = new IPEndPoint(parsedAddress, parsedPort);
            var address = Guid.NewGuid().ToString();
            A.CallTo(() => _endpointParser.Parse(address)).Returns(endpoint);
            _multiplayerClient.Connect(address);
            _multiplayerClient.Game = new NetworkGame(new NetworkGameStartUp("whatever"));
            var handler = FakeUtils.GetNetworkReceiverHandler<NotifyLobbySaveGameChanged>(_networkClient);
            var request = new NotifyLobbySaveGameChanged { Content = [], GameId = Guid.NewGuid().ToString() };

            // Act
            handler.Invoke(1, request);

            // Assert
            A.CallTo(() => _fileSystemService.WriteFile(A<string>.Ignored, request.Content)).MustHaveHappened();
            A.CallTo(() => _networkClient.Send(A<NotifyLobbySyncStatusChanged>.Ignored)).MustHaveHappened();
        }

        [Test]
        public void OnGameForceLoaded_PlayerIsInGame_StoresSaveFileAnCallsQuickLoad()
        {
            // Arrange
            var parsedHost = "192.168.1.1";
            var parsedPort = 555;
            IPAddress.TryParse(parsedHost, out var parsedAddress);
            var endpoint = new IPEndPoint(parsedAddress, parsedPort);
            var address = Guid.NewGuid().ToString();
            A.CallTo(() => _endpointParser.Parse(address)).Returns(endpoint);
            _multiplayerClient.Connect(address);
            _multiplayerClient.Game = new NetworkGame(new NetworkGameStartUp("whatever"))
            {
                Stage = NetworkLobbyStage.Playing
            };
            var handler = FakeUtils.GetNetworkReceiverHandler<NotifyGameForceLoaded>(_networkClient);
            var request = new NotifyGameForceLoaded { Content = [], GameId = Guid.NewGuid().ToString() };

            // Act
            handler.Invoke(1, request);

            // Assert
            A.CallTo(() => _fileSystemService.WriteFile(A<string>.Ignored, request.Content)).MustHaveHappened();
            A.CallTo(() => _gameInteractionService.QuickLoadGame(A<string>.Ignored)).MustHaveHappened();
            A.CallTo(() => _networkClient.Send(A<NotifyLobbySyncStatusChanged>.Ignored)).MustNotHaveHappened();
        }

        [Test]
        public void OnGameForceLoaded_PlayerJoinedMidGame_MakesPlayerReadyAndCallsLoadFromMainMenu()
        {
            // Arrange
            var parsedHost = "192.168.1.1";
            var parsedPort = 555;
            var localPlayerId = 1231231;
            IPAddress.TryParse(parsedHost, out var parsedAddress);
            var endpoint = new IPEndPoint(parsedAddress, parsedPort);
            var address = Guid.NewGuid().ToString();
            A.CallTo(() => _endpointParser.Parse(address)).Returns(endpoint);
            A.CallTo(() => _gameInteractionService.GetSaveGamePath()).Returns("asdasdas");
            A.CallTo(() => _fileSystemService.WriteFile(A<string>.Ignored, A<byte[]>.Ignored)).Returns(true);
            _multiplayerClient.Connect(address);
            _multiplayerClient.Game = new NetworkGame(new NetworkGameStartUp("whatever"))
            {
                LocalPlayerId = localPlayerId,
                Stage = NetworkLobbyStage.Lobby,
                Players = [new NetworkPlayer { Id = localPlayerId }]
            };
            var handler = FakeUtils.GetNetworkReceiverHandler<NotifyGameForceLoaded>(_networkClient);
            var request = new NotifyGameForceLoaded { Content = [], GameId = Guid.NewGuid().ToString() };

            // Act
            handler.Invoke(1, request);

            // Assert
            A.CallTo(() => _fileSystemService.WriteFile(A<string>.Ignored, request.Content)).MustHaveHappened();
            A.CallTo(() => _gameInteractionService.LoadGameFromMainMenu(A<string>.That.Not.IsNullOrEmpty())).MustHaveHappened();
            A.CallTo(() => _networkClient.Send(A<NotifyPlayerReadyStatusChanged>.Ignored)).MustHaveHappened();
            A.CallTo(() => _networkClient.Send(A<NotifyLobbySyncStatusChanged>.Ignored)).MustHaveHappened();
        }
    }
}