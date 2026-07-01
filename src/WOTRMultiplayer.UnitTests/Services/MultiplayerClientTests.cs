using System;
using AutoMapper;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.IO;
using WOTRMultiplayer.Abstractions.Random;
using WOTRMultiplayer.Abstractions.Settings;
using WOTRMultiplayer.Config.Mapping;
using WOTRMultiplayer.Networking.Abstractions;
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
        private IFileSystemService _fileSystemService;
        private INetworkClientConnection _networkClient;
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
            _multiplayerSettingsProvider = A.Fake<IMultiplayerSettingsService>();
            _fileSystemService = A.Fake<IFileSystemService>();

            _networkClient = A.Fake<INetworkClientConnection>();
            Fake.GetFakeManager(_networkClient).AddRuleFirst(new NetworkReceiverFakeRule<INetworkConnection>());

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
                _multiplayerSettingsProvider,
                _fileSystemService,
                _networkClient,
                _valueGenerator,
                _mapper);
        }

        [Test]
        public void Connect_ValidAddress_CallsConnectOnNetworkClient()
        {
            // Arrange
            var host = "192.168.1.1";
            var port = 555;

            // Act
            _multiplayerClient.Connect(host, port);

            // Assert
            A.CallTo(() => _networkClient.ConnectAsync(host, port, A<TimeSpan>.Ignored)).MustHaveHappenedOnceExactly();
        }
    }
}