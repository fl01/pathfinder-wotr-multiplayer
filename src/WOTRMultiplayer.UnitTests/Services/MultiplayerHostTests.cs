using System;
using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using FakeItEasy;
using Kingmaker.GameModes;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.IO;
using WOTRMultiplayer.Abstractions.Random;
using WOTRMultiplayer.Abstractions.Settings;
using WOTRMultiplayer.Config.Mapping;
using WOTRMultiplayer.Entities;
using WOTRMultiplayer.Entities.Content;
using WOTRMultiplayer.Entities.Items;
using WOTRMultiplayer.Entities.Settings;
using WOTRMultiplayer.Networking;
using WOTRMultiplayer.Networking.Abstractions;
using WOTRMultiplayer.Networking.Messages.Game;
using WOTRMultiplayer.Networking.Messages.Lobby;
using WOTRMultiplayer.Services;
using WOTRMultiplayer.UnitTests.FakeRules;

namespace WOTRMultiplayer.UnitTests.Services
{
    [TestFixture]
    public class MultiplayerHostTests
    {
        private MultiplayerHost _multiplayerHost;

        private ILogger<MultiplayerHost> _logger;
        private IGameInteractionService _gameInteractionService;
        private ILevelingInteractionService _levelingInteractionService;
        private IPlayerNotificationService _playerNotificationService;
        private IDialogInteractionService _dialogInteractionService;
        private IGlobalMapInteractionService _globalMapInteractionService;
        private IPingInteractionService _pingInteractionService;
        private ICombatInteractionService _combatInteractionService;
        private IMultiplayerSettingsService _multiplayerSettingsProvider;
        private IFileSystemService _fileSystemService;
        private INetworkServer _networkServer;
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
            _levelingInteractionService = A.Fake<ILevelingInteractionService>();
            _playerNotificationService = A.Fake<IPlayerNotificationService>();
            _dialogInteractionService = A.Fake<IDialogInteractionService>();
            _globalMapInteractionService = A.Fake<IGlobalMapInteractionService>();
            _pingInteractionService = A.Fake<IPingInteractionService>();
            _combatInteractionService = A.Fake<ICombatInteractionService>();
            _multiplayerSettingsProvider = A.Fake<IMultiplayerSettingsService>();
            _fileSystemService = A.Fake<IFileSystemService>();

            _networkServer = A.Fake<INetworkServer>();
            Fake.GetFakeManager(_networkServer).AddRuleFirst(new NetworkReceiverFakeRule());

            _valueGenerator = A.Fake<IValueGenerator>();

            _multiplayerHost = new MultiplayerHost(
                _logger,
                _gameInteractionService,
                _levelingInteractionService,
                _playerNotificationService,
                _dialogInteractionService,
                _globalMapInteractionService,
                _pingInteractionService,
                _combatInteractionService,
                _multiplayerSettingsProvider,
                _fileSystemService,
                _networkServer,
                _valueGenerator,
                _mapper);
        }

        [Test]
        public void Create_NonEmptySavePathAndGameId_CallsNetworkServerStart()
        {
            // Arrange
            var startUp = new NetworkGameStartUp() { SavePath = Guid.NewGuid().ToString(), IsNewGameSequence = false };
            var gameId = Guid.NewGuid().ToString();
            var settings = new NetworkMultiplayerSettings { HostPortRangeStart = 123, HostPortRangeEnd = 1234, NetworkAwaiterTimeout = TimeSpan.FromMinutes(1) };
            A.CallTo(() => _multiplayerSettingsProvider.GetSettings()).Returns(settings);

            // Act
            _multiplayerHost.Create(gameId, startUp);

            // Assert
            A.CallTo(() => _networkServer.Start(settings.HostPortRangeStart, settings.HostPortRangeEnd, settings.NetworkAwaiterTimeout)).MustHaveHappenedOnceExactly();
        }

        [Test]
        public void OnNotifyDropItem__CallsGameInteraction()
        {
            // Arrange
            var startUp = new NetworkGameStartUp() { SavePath = Guid.NewGuid().ToString(), IsNewGameSequence = false };
            var gameId = Guid.NewGuid().ToString();
            var settings = new NetworkMultiplayerSettings() { HostPortRangeStart = 123, HostPortRangeEnd = 1234 };
            A.CallTo(() => _multiplayerSettingsProvider.GetSettings()).Returns(settings);
            _multiplayerHost.Create(gameId, startUp);
            var handler = FakeUtils.GetNetworkReceiverHandler<NotifyDropItem>(_networkServer);
            var request = new NotifyDropItem { Drop = new Networking.Messages.Contracts.NetworkDropItem { Item = new Networking.Messages.Contracts.NetworkItem() } };
            var playerId = 123;

            // Act
            handler.Invoke(playerId, request);

            // Assert
            A.CallTo(() => _gameInteractionService.DropItem(A<NetworkDropItem>.Ignored)).MustHaveHappenedOnceExactly();
        }

        [Test]
        public void OnNotifyRestEnded__AddsToReadyListAndCallsGameInteractionService()
        {
            // Arrange
            var startUp = new NetworkGameStartUp() { SavePath = Guid.NewGuid().ToString(), IsNewGameSequence = false };
            var gameId = Guid.NewGuid().ToString();
            var settings = new NetworkMultiplayerSettings() { HostPortRangeStart = 123, HostPortRangeEnd = 1234 };
            A.CallTo(() => _multiplayerSettingsProvider.GetSettings()).Returns(settings);
            _multiplayerHost.Create(gameId, startUp);
            _multiplayerHost.Game.Rest = new NetworkRest();
            var handler = FakeUtils.GetNetworkReceiverHandler<NotifyRestEnded>(_networkServer);
            var playerId = 123;
            var message = new NotifyRestEnded { PlayerId = playerId };

            // Act
            handler.Invoke(playerId, message);

            // Assert
            A.CallTo(() => _gameInteractionService.UpdateRestUI(true, 1, 0)).MustHaveHappenedOnceExactly();
            Assert.That(_multiplayerHost.Game.Rest.PlayersFinishedRest, Contains.Item(playerId));
        }

        [Test]
        public void ClientGameServerConnectionConfirmed_PlayerNameExists_PlayerNameIsUpdated()
        {
            // Arrange
            var startUp = new NetworkGameStartUp() { SavePath = Guid.NewGuid().ToString(), IsNewGameSequence = false };
            var gameId = Guid.NewGuid().ToString();
            var settings = new NetworkMultiplayerSettings() { HostPortRangeStart = 123, HostPortRangeEnd = 1234 };
            A.CallTo(() => _multiplayerSettingsProvider.GetSettings()).Returns(settings);
            _multiplayerHost.Create(gameId, startUp);
            var hostPlayer = new NetworkPlayer { Id = NetworkingConsts.HostPlayerId, IsHost = true };
            var player = new NetworkPlayer { Id = 123 };
            _multiplayerHost.Game.Players.AddRange([hostPlayer, player]);
            var handler = FakeUtils.GetNetworkReceiverHandler<ClientGameServerConnectionConfirmed>(_networkServer);
            var message = new ClientGameServerConnectionConfirmed
            {
                PlayerName = Guid.NewGuid().ToString(),
                ContentState = new Networking.Messages.Contracts.NetworkContentState()
            };

            // Act
            handler.Invoke(player.Id, message);

            // Assert
            Assert.That(player.Name, Is.EqualTo(message.PlayerName));
        }

        [Test]
        public void ClientGameServerConnectionConfirmed_BothHaveEmptyContentState_PlayerStateRemainsUnchanged()
        {
            // Arrange
            var startUp = new NetworkGameStartUp() { SavePath = Guid.NewGuid().ToString(), IsNewGameSequence = false };
            var gameId = Guid.NewGuid().ToString();
            var settings = new NetworkMultiplayerSettings() { HostPortRangeStart = 123, HostPortRangeEnd = 1234 };
            A.CallTo(() => _multiplayerSettingsProvider.GetSettings()).Returns(settings);
            _multiplayerHost.Create(gameId, startUp);
            var hostPlayer = new NetworkPlayer { Id = NetworkingConsts.HostPlayerId, IsHost = true };
            var player = new NetworkPlayer { Id = 123 };
            _multiplayerHost.Game.Players.AddRange([hostPlayer, player]);
            var handler = FakeUtils.GetNetworkReceiverHandler<ClientGameServerConnectionConfirmed>(_networkServer);
            var message = new ClientGameServerConnectionConfirmed
            {
                PlayerName = Guid.NewGuid().ToString(),
                ContentState = new Networking.Messages.Contracts.NetworkContentState()
            };

            // Act
            handler.Invoke(player.Id, message);

            // Assert
            Assert.That(player.ContentState.DiscrepantDLCs, Has.Count.EqualTo(0));
            Assert.That(player.ContentState.DiscrepantMods, Has.Count.EqualTo(0));
        }

        [TestCaseSource(nameof(DlcDifferencesTestCases))]
        public void ClientGameServerConnectionConfirmed_DlcContentStateIsDifferent_DiscrepantListContainsCorrectReasonAndNumberOfDLCs(ContentStateTestCase testCase)
        {
            // Arrange
            var startUp = new NetworkGameStartUp() { SavePath = Guid.NewGuid().ToString(), IsNewGameSequence = false };
            var gameId = Guid.NewGuid().ToString();
            var settings = new NetworkMultiplayerSettings() { HostPortRangeStart = 123, HostPortRangeEnd = 1234 };
            A.CallTo(() => _multiplayerSettingsProvider.GetSettings()).Returns(settings);
            _multiplayerHost.Create(gameId, startUp);
            var hostPlayer = new NetworkPlayer
            {
                Id = NetworkingConsts.HostPlayerId,
                IsHost = true,
                ContentState = new NetworkContentState
                {
                    DLCs = testCase.HostDLCs,
                }
            };
            var player = new NetworkPlayer { Id = 123 };
            _multiplayerHost.Game.Players.AddRange([hostPlayer, player]);
            var handler = FakeUtils.GetNetworkReceiverHandler<ClientGameServerConnectionConfirmed>(_networkServer);
            var message = new ClientGameServerConnectionConfirmed
            {
                PlayerName = Guid.NewGuid().ToString(),
                ContentState = new Networking.Messages.Contracts.NetworkContentState
                {
                    DLCs = testCase.PlayerDLCs
                },
            };

            // Act
            handler.Invoke(player.Id, message);

            // Assert
            Assert.That(player.ContentState.DiscrepantDLCs, Has.Count.EqualTo(testCase.ExpectedDiscrepantDLCs.Count));
            foreach (var expectedDiscrepantDLC in testCase.ExpectedDiscrepantDLCs)
            {
                var actualDiscrepantDLC = player.ContentState.DiscrepantDLCs.FirstOrDefault(p => p.DLC.Id == expectedDiscrepantDLC.DLC.Id);
                Assert.That(actualDiscrepantDLC, Is.Not.Null);
                Assert.That(actualDiscrepantDLC.Reason, Is.EqualTo(expectedDiscrepantDLC.Reason));
            }
        }

        [TestCaseSource(nameof(ModDifferencesTestCases))]
        public void ClientGameServerConnectionConfirmed_ModContentStateIsDifferent_DiscrepantListContainsCorrectReasonAndNumberOfDLCs(ContentStateTestCase testCase)
        {
            // Arrange
            var startUp = new NetworkGameStartUp() { SavePath = Guid.NewGuid().ToString(), IsNewGameSequence = false };
            var gameId = Guid.NewGuid().ToString();
            var settings = new NetworkMultiplayerSettings() { HostPortRangeStart = 123, HostPortRangeEnd = 1234 };
            A.CallTo(() => _multiplayerSettingsProvider.GetSettings()).Returns(settings);
            _multiplayerHost.Create(gameId, startUp);
            var hostPlayer = new NetworkPlayer
            {
                Id = NetworkingConsts.HostPlayerId,
                IsHost = true,
                ContentState = new NetworkContentState
                {
                    Mods = testCase.HostMods,
                }
            };
            var player = new NetworkPlayer { Id = 123 };
            _multiplayerHost.Game.Players.AddRange([hostPlayer, player]);
            var handler = FakeUtils.GetNetworkReceiverHandler<ClientGameServerConnectionConfirmed>(_networkServer);
            var message = new ClientGameServerConnectionConfirmed
            {
                PlayerName = Guid.NewGuid().ToString(),
                ContentState = new Networking.Messages.Contracts.NetworkContentState
                {
                    Mods = testCase.PlayerMods
                },
            };

            // Act
            handler.Invoke(player.Id, message);

            // Assert
            Assert.That(player.ContentState.DiscrepantMods, Has.Count.EqualTo(testCase.ExpectedDiscrepantMods.Count));
            foreach (var expectedDiscrepantMod in testCase.ExpectedDiscrepantMods)
            {
                var actualDiscrepantMod = player.ContentState.DiscrepantMods.FirstOrDefault(p => p.Id == expectedDiscrepantMod.Id);
                Assert.That(actualDiscrepantMod, Is.Not.Null);
                Assert.That(actualDiscrepantMod.Reason, Is.EqualTo(expectedDiscrepantMod.Reason));
            }
        }

        [TestCase(GameModeType.Enum.None)]
        [TestCase(GameModeType.Enum.Default)]
        [TestCase(GameModeType.Enum.Cutscene)]
        [TestCase(GameModeType.Enum.CutsceneGlobalMap)]
        [TestCase(GameModeType.Enum.GameOver)]
        [TestCase(GameModeType.Enum.Dialog)]
        [TestCase(GameModeType.Enum.Rest)]
        public void OnStartGameMode_ModeIsRegistered(GameModeType.Enum mode)
        {
            // Arrange
            var gameMode = GameModeType.All.First(x => x.Index == (int)mode);
            _multiplayerHost.Game = new NetworkGame(new NetworkGameStartUp())
            {
                LocalPlayerId = 12312313
            };

            // Act
            _multiplayerHost.OnStartGameMode(gameMode);

            // Assert
            _multiplayerHost.Game.PlayersInGameMode.TryGetValue(gameMode, out var players);
            Assert.That(players, Is.Not.Empty);
            Assert.That(players, Does.Contain(_multiplayerHost.Game.LocalPlayerId));
        }

        [Test]
        public void OnStartGameMode_RestMode_RestUIIsUpdated()
        {
            // Arrange
            var gameMode = GameModeType.All.First(x => x.Index == (int)GameModeType.Enum.Rest);
            _multiplayerHost.Game = new NetworkGame(new NetworkGameStartUp())
            {
                LocalPlayerId = NetworkingConsts.HostPlayerId,
                Players = [
                    new NetworkPlayer { Id = NetworkingConsts.HostPlayerId, LobbySyncStatus = NetworkLobbySyncStatus.Succeed },
                    new NetworkPlayer { Id = 123123, LobbySyncStatus = NetworkLobbySyncStatus.Succeed },
                    ]
            };

            // Act
            _multiplayerHost.OnStartGameMode(gameMode);

            // Assert
            _multiplayerHost.Game.PlayersInGameMode.TryGetValue(gameMode, out var players);
            A.CallTo(() => _gameInteractionService.UpdateRestUI(false, players.Count, _multiplayerHost.Game.Players.Count)).MustHaveHappened();
        }

        private static IEnumerable<ContentStateTestCase> DlcDifferencesTestCases()
        {
            const string dlcId1 = "dlc1";
            yield return new ContentStateTestCase("dlc is available on host, but unavailable at player")
            {
                HostDLCs = [new NetworkDLC { Id = dlcId1, IsAvailable = true }],
                PlayerDLCs = [new Networking.Messages.Contracts.NetworkDLC { Id = dlcId1, IsAvailable = false }],
                ExpectedDiscrepantDLCs = [new NetworkDiscrepantDLC { DLC = new NetworkDLC { Id = dlcId1 }, Reason = NetworkDiscrepancyReason.Missing }]
            };

            yield return new ContentStateTestCase("dlc is unavailable on host, but available at player")
            {
                HostDLCs = [new NetworkDLC { Id = dlcId1, IsAvailable = false }],
                PlayerDLCs = [new Networking.Messages.Contracts.NetworkDLC { Id = dlcId1, IsAvailable = true }],
                ExpectedDiscrepantDLCs = [new NetworkDiscrepantDLC { DLC = new NetworkDLC { Id = dlcId1 }, Reason = NetworkDiscrepancyReason.Extra }]
            };

            yield return new ContentStateTestCase("dlc is unavailable for both")
            {
                HostDLCs = [new NetworkDLC { Id = dlcId1, IsAvailable = false }],
                PlayerDLCs = [new Networking.Messages.Contracts.NetworkDLC { Id = dlcId1, IsAvailable = false }],
                ExpectedDiscrepantDLCs = []
            };

            yield return new ContentStateTestCase("dlc is available for both")
            {
                HostDLCs = [new NetworkDLC { Id = dlcId1, IsAvailable = true }],
                PlayerDLCs = [new Networking.Messages.Contracts.NetworkDLC { Id = dlcId1, IsAvailable = true }],
                ExpectedDiscrepantDLCs = []
            };
        }

        private static IEnumerable<ContentStateTestCase> ModDifferencesTestCases()
        {
            const string modId1 = "mod1";
            yield return new ContentStateTestCase("mod is enabled on host, but disabled at player")
            {
                HostMods = [new NetworkMod { Id = modId1, IsEnabled = true }],
                PlayerMods = [new Networking.Messages.Contracts.NetworkMod { Id = modId1, IsEnabled = false }],
                ExpectedDiscrepantMods = [new NetworkDiscrepantMod(modId1, NetworkModType.UnityModManager, null, null, NetworkDiscrepancyReason.Disabled)]
            };

            yield return new ContentStateTestCase("mod is disabled on host, but enabled at player")
            {
                HostMods = [new NetworkMod { Id = modId1, IsEnabled = false }],
                PlayerMods = [new Networking.Messages.Contracts.NetworkMod { Id = modId1, IsEnabled = true }],
                ExpectedDiscrepantMods = [new NetworkDiscrepantMod(modId1, NetworkModType.UnityModManager, null, null, NetworkDiscrepancyReason.Extra)]
            };

            yield return new ContentStateTestCase("mod is installed and enabled on host, but it's not installed at player")
            {
                HostMods = [new NetworkMod { Id = modId1, IsEnabled = true }],
                PlayerMods = [],
                ExpectedDiscrepantMods = [new NetworkDiscrepantMod(modId1, NetworkModType.UnityModManager, null, null, NetworkDiscrepancyReason.Missing)]
            };

            yield return new ContentStateTestCase("mod is disabled for both")
            {
                HostMods = [new NetworkMod { Id = modId1, IsEnabled = false }],
                PlayerMods = [new Networking.Messages.Contracts.NetworkMod { Id = modId1, IsEnabled = false }],
                ExpectedDiscrepantMods = []
            };

            yield return new ContentStateTestCase("mod is not installed on host, but it's installed and disabled at player")
            {
                HostMods = [],
                PlayerMods = [new Networking.Messages.Contracts.NetworkMod { Id = modId1, IsEnabled = false }],
                ExpectedDiscrepantMods = []
            };

            yield return new ContentStateTestCase("mod is installed and disabled on host, but it's not installed at player")
            {
                HostMods = [new NetworkMod { Id = modId1, IsEnabled = false }],
                PlayerMods = [],
                ExpectedDiscrepantMods = []
            };

            yield return new ContentStateTestCase("mod is installed and enabled for both")
            {
                HostMods = [new NetworkMod { Id = modId1, IsEnabled = true }],
                PlayerMods = [new Networking.Messages.Contracts.NetworkMod { Id = modId1, IsEnabled = true }],
                ExpectedDiscrepantMods = []
            };

            yield return new ContentStateTestCase("different mod version")
            {
                HostMods = [new NetworkMod { Id = modId1, IsEnabled = true, Version = "1111" }],
                PlayerMods = [new Networking.Messages.Contracts.NetworkMod { Id = modId1, IsEnabled = true, Version = "2222" }],
                ExpectedDiscrepantMods = [new NetworkDiscrepantMod(modId1, NetworkModType.UnityModManager, "2222", "1111", NetworkDiscrepancyReason.VersionMismatch)]
            };
        }
    }
}
