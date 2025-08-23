using System;
using AutoMapper;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.IO;
using WOTRMultiplayer.Abstractions.MP;
using WOTRMultiplayer.Abstractions.Random;
using WOTRMultiplayer.Config.Mapping;
using WOTRMultiplayer.MP;
using WOTRMultiplayer.MP.Actors;
using WOTRMultiplayer.MP.Entities.Loot;
using WOTRMultiplayer.Networking.Abstractions;
using WOTRMultiplayer.Networking.Messages.Game;
using WOTRMultiplayer.UnitTests.FakeRules;

namespace WOTRMultiplayer.UnitTests.MP
{
    [TestFixture]
    public class MultiplayerHostTests
    {
        private MultiplayerHost _multiplayerHost;

        private ILogger<MultiplayerHost> _logger;
        private IGameInteractionService _gameInteractionService;
        private IMultiplayerSettingsProvider _multiplayerSettingsProvider;
        private IIPEndPointParser _endpointParser;
        private IFileSystemService _fileSystemService;
        private INetworkServer _networkServer;
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

            _logger = A.Fake<ILogger<MultiplayerHost>>();
            _gameInteractionService = A.Fake<IGameInteractionService>();
            _endpointParser = A.Fake<IIPEndPointParser>();
            _multiplayerSettingsProvider = A.Fake<IMultiplayerSettingsProvider>();
            _fileSystemService = A.Fake<IFileSystemService>();

            _networkServer = A.Fake<INetworkServer>();
            Fake.GetFakeManager(_networkServer).AddRuleFirst(new NetworkReceiverFakeRule());

            _diceRollStorage = A.Fake<IDiceRollStorage>();
            _valueGenerator = A.Fake<IValueGenerator>();

            _multiplayerHost = new MultiplayerHost(
                _logger,
                _gameInteractionService,
                _multiplayerSettingsProvider,
                _fileSystemService,
                _networkServer,
                _diceRollStorage,
                _valueGenerator,
                _mapper);
        }

        [Test]
        public void Create_NonEmptySavePathAndGameId_CallsNetworkServerStart()
        {
            // Arrange
            var savePath = Guid.NewGuid().ToString();
            var gameId = Guid.NewGuid().ToString();
            var settings = new MultiplayerSettings() { HostPortRangeStart = 123, HostPortRangeEnd = 1234 };
            A.CallTo(() => _multiplayerSettingsProvider.Settings).Returns(settings);

            // Act
            _multiplayerHost.Create(savePath, gameId, []);

            // Assert
            A.CallTo(() => _networkServer.Start(settings.HostPortRangeStart, settings.HostPortRangeEnd)).MustHaveHappenedOnceExactly();
        }

        [Test]
        public void OnNotifyDropItem__CallsGameInteractionAndResendsMessage()
        {
            // Arrange
            var savePath = Guid.NewGuid().ToString();
            var gameId = Guid.NewGuid().ToString();
            var settings = new MultiplayerSettings() { HostPortRangeStart = 123, HostPortRangeEnd = 1234 };
            A.CallTo(() => _multiplayerSettingsProvider.Settings).Returns(settings);
            _multiplayerHost.Create(savePath, gameId, []);
            var handler = FakeUtils.GetNetworkReceiverHandler<NotifyDropItem>(_networkServer);
            var request = new NotifyDropItem { Drop = new Networking.Messages.Contracts.NetworkDropItem { Item = new Networking.Messages.Contracts.NetworkItem() } };
            var playerId = 123;

            // Act
            handler.Invoke(playerId, request);

            // Assert
            A.CallTo(() => _gameInteractionService.DropItem(A<NetworkDropItem>.Ignored)).MustHaveHappenedOnceExactly();
            A.CallTo(() => _networkServer.SendAllExcept(playerId, request)).MustHaveHappenedOnceExactly();
        }
    }
}
