using System;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using WOTRMultiplayer.Abstractions;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.Random;
using WOTRMultiplayer.Abstractions.UI;
using WOTRMultiplayer.Abstractions.UI.Controllers;
using WOTRMultiplayer.Abstractions.UI.Windows;
using WOTRMultiplayer.Entities;
using WOTRMultiplayer.Services;
using WOTRMultiplayer.UI.Menu;

namespace WOTRMultiplayer.UnitTests.Services
{
    [TestFixture]
    public class MultiplayerTests
    {
        private Multiplayer _multiplayer;
        private ILogger<Multiplayer> _logger;
        private IUIFactory _uiFactory;
        private ILobbyWindowController _lobbyWindowController;
        private IMultiplayerHost _multiplayerHost;
        private IMultiplayerClient _multiplayerClient;
        private IGameInteractionService _gameInteractionService;
        private IValueGenerator _idGenerator;
        private IMultiplayerActorAccessor _multiplerActorAccessor;

        [SetUp]
        public void SetUp()
        {
            _logger = A.Fake<ILogger<Multiplayer>>();
            _uiFactory = A.Fake<IUIFactory>();
            _lobbyWindowController = A.Fake<ILobbyWindowController>();
            _multiplayerHost = A.Fake<IMultiplayerHost>();
            _multiplayerClient = A.Fake<IMultiplayerClient>();
            _gameInteractionService = A.Fake<IGameInteractionService>();
            _idGenerator = A.Fake<IValueGenerator>();
            _multiplerActorAccessor = A.Fake<IMultiplayerActorAccessor>();

            _multiplayer = new Multiplayer(
                _logger,
                _uiFactory,
                _lobbyWindowController,
                _multiplerActorAccessor,
                _gameInteractionService,
                _idGenerator);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void IsActive_TestCase_DependsOnAccessor(bool actorStatus)
        {
            // Arrange
            var current = A.Fake<IMultiplayerActor>();
            A.CallTo(() => current.IsActive).Returns(actorStatus);
            A.CallTo(() => _multiplerActorAccessor.Current).Returns(current);

            // Act
            var actualStatus = _multiplayer.IsActive;

            // Assert
            Assert.That(actualStatus, Is.EqualTo(actorStatus));
        }

        [Test]
        public void InitializeMultiplayer_HostAndClientAreNotActive_CallsFactoryWithoutCallingDispose()
        {
            // Arrange
            var context = new InitializeMultiplayerContext(null, null);
            A.CallTo(() => _multiplayerHost.IsActive).Returns(false);
            A.CallTo(() => _multiplayerClient.IsActive).Returns(false);

            // Act
            _multiplayer.InitializeMultiplayer(context);

            // Assert
            A.CallTo(() => _uiFactory.InitializeMultiplayerWindow(context, An<Action>.Ignored)).MustHaveHappenedOnceExactly();
            A.CallTo(() => _multiplayerHost.Reset()).MustNotHaveHappened();
            A.CallTo(() => _multiplayerClient.Reset()).MustNotHaveHappened();
        }

        [Test]
        public void InitializeMultiplayer_MultiplayerHostIsActive_LogsAndCallsDispose()
        {
            // Arrange
            A.CallTo(() => _multiplerActorAccessor.Host).Returns(_multiplayerHost);
            A.CallTo(() => _multiplerActorAccessor.Client).Returns(_multiplayerClient);
            var context = new InitializeMultiplayerContext(null, null);
            A.CallTo(() => _multiplayerHost.IsActive).Returns(true);

            // Act
            _multiplayer.InitializeMultiplayer(context);

            // Assert
            A.CallTo(() => _multiplayerHost.Reset()).MustHaveHappenedOnceExactly();
        }

        [Test]
        public void TerminateMultiplayer_DisposesEverything()
        {
            // Arrange
            A.CallTo(() => _multiplerActorAccessor.Host).Returns(_multiplayerHost);
            A.CallTo(() => _multiplerActorAccessor.Client).Returns(_multiplayerClient);

            // Act
            _multiplayer.TerminateMultiplayer();

            // Assert
            A.CallTo(() => _multiplayerClient.Reset()).MustHaveHappenedOnceExactly();
            A.CallTo(() => _multiplayerHost.Reset()).MustHaveHappenedOnceExactly();
            A.CallTo(() => _lobbyWindowController.ResetOwnerContent(LobbyWindowOwner.EscMenu)).MustHaveHappenedOnceExactly();
            A.CallTo(() => _uiFactory.DestroyLobbyWindow(An<ILobbyWindow>.Ignored)).MustHaveHappenedOnceExactly();
        }

        [Test]
        public void InitializeMultiplayer_MultiplayerClientIsActive_LogsAndCallsDispose()
        {
            // Arrange
            A.CallTo(() => _multiplerActorAccessor.Host).Returns(_multiplayerHost);
            A.CallTo(() => _multiplerActorAccessor.Client).Returns(_multiplayerClient);
            var context = new InitializeMultiplayerContext(null, null);
            A.CallTo(() => _multiplayerClient.IsActive).Returns(true);

            // Act
            _multiplayer.InitializeMultiplayer(context);

            // Assert
            A.CallTo(() => _multiplayerClient.Reset()).MustHaveHappenedOnceExactly();
        }
    }
}
