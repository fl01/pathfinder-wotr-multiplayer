using System;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using WOTRMultiplayer.Abstractions.MP;
using WOTRMultiplayer.Abstractions.UI;
using WOTRMultiplayer.Abstractions.UI.Controllers;
using WOTRMultiplayer.Abstractions.UI.Windows;
using WOTRMultiplayer.MP;
using WOTRMultiplayer.MP.Entities;
using WOTRMultiplayer.UI.Lobby;

namespace WOTRMultiplayer.UnitTests.MP
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
        private IRollStorage _rollStorage;

        [SetUp]
        public void SetUp()
        {
            _logger = A.Fake<ILogger<Multiplayer>>();
            _uiFactory = A.Fake<IUIFactory>();
            _lobbyWindowController = A.Fake<ILobbyWindowController>();
            _multiplayerHost = A.Fake<IMultiplayerHost>();
            _multiplayerClient = A.Fake<IMultiplayerClient>();
            _rollStorage = A.Fake<IRollStorage>();

            _multiplayer = new Multiplayer(
                _logger,
                _uiFactory,
                _lobbyWindowController,
                _multiplayerHost,
                _multiplayerClient,
                _rollStorage);
        }

        [TestCase(true, true, true)]
        [TestCase(true, false, true)]
        [TestCase(false, true, true)]
        [TestCase(false, false, false)]
        public void IsActive_TestCase_DependsOnHostAndClientIsActive(bool hostStatus, bool clientStatus, bool expectedStatus)
        {
            // Arrange
            A.CallTo(() => _multiplayerHost.IsActive).Returns(hostStatus);
            A.CallTo(() => _multiplayerClient.IsActive).Returns(clientStatus);

            // Act
            var actualStatus = _multiplayer.IsActive;

            // Assert
            Assert.That(actualStatus, Is.EqualTo(expectedStatus));
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
            A.CallTo(() => _multiplayerHost.Dispose()).MustNotHaveHappened();
            A.CallTo(() => _multiplayerClient.Dispose()).MustNotHaveHappened();
        }

        [Test]
        public void InitializeMultiplayer_MultiplayerHostIsActive_LogsAndCallsDispose()
        {
            // Arrange
            var context = new InitializeMultiplayerContext(null, null);
            A.CallTo(() => _multiplayerHost.IsActive).Returns(true);

            // Act
            _multiplayer.InitializeMultiplayer(context);

            // Assert
            A.CallTo(() => _multiplayerHost.Dispose()).MustHaveHappenedOnceExactly();
        }

        [Test]
        public void TerminateMultiplayer_DisposesEverything()
        {
            // Arrange

            // Act
            _multiplayer.TerminateMultiplayer();

            // Assert
            A.CallTo(() => _multiplayerClient.Dispose()).MustHaveHappenedOnceExactly();
            A.CallTo(() => _multiplayerHost.Dispose()).MustHaveHappenedOnceExactly();
            A.CallTo(() => _lobbyWindowController.ResetOwnerContent(LobbyWindowOwner.EscMenu)).MustHaveHappenedOnceExactly();
            A.CallTo(() => _uiFactory.DestroyLobbyWindow(An<ILobbyWindow>.Ignored)).MustHaveHappenedOnceExactly();
        }

        [TestCase(true)]
        [TestCase(false)]
        public void InitializeEscMenuLobbyWindow_CallsFactory(bool isHostActive)
        {
            // Arrange
            var context = new InitializeEscMenuLobbyWindowContext(null);
            A.CallTo(() => _multiplayerHost.IsActive).Returns(isHostActive);

            // Act
            _multiplayer.InitializeEscMenuLobbyWindow(context);

            // Assert
            A.CallTo(() => _uiFactory.InitializeEscMenuLobbyWindow(context, _multiplayerHost.IsActive, An<Action>.Ignored)).MustHaveHappenedOnceExactly();
        }


        [Test]
        public void InitializeEscMenuLobbyWindow_ConfiguresLobbyWindow()
        {
            // Arrange
            var context = new InitializeEscMenuLobbyWindowContext(null);
            var windowFake = A.Fake<ILobbyWindow>();
            A.CallTo(() => _uiFactory.InitializeEscMenuLobbyWindow(An<InitializeEscMenuLobbyWindowContext>.Ignored, An<bool>.Ignored, An<Action>.Ignored)).Returns(windowFake);

            // Act
            _multiplayer.InitializeEscMenuLobbyWindow(context);

            // Assert
            A.CallTo(() => windowFake.AssignLobbyController(_lobbyWindowController)).MustHaveHappenedOnceExactly();
            Assert.That(windowFake.NetworkGame, Is.Not.Null);
        }

        [Test]
        public void InitializeEscMenuLobbyWindow_HostIsActive_NetworkGameIsTakenFromHost()
        {
            // Arrange
            var context = new InitializeEscMenuLobbyWindowContext(null);
            var windowFake = A.Fake<ILobbyWindow>();
            A.CallTo(() => _multiplayerHost.IsActive).Returns(true);
            var expectedGame = A.Fake<NetworkGame>();
            A.CallTo(() => _multiplayerHost.CurrentGame).Returns(expectedGame);
            A.CallTo(() => _uiFactory.InitializeEscMenuLobbyWindow(An<InitializeEscMenuLobbyWindowContext>.Ignored, An<bool>.Ignored, An<Action>.Ignored)).Returns(windowFake);

            // Act
            _multiplayer.InitializeEscMenuLobbyWindow(context);
            var actual = windowFake.NetworkGame();

            // Assert
            Assert.That(actual, Is.EqualTo(expectedGame));
        }


        [Test]
        public void InitializeEscMenuLobbyWindow_ClientIsActive_NetworkGameIsTakenFromClient()
        {
            // Arrange
            var context = new InitializeEscMenuLobbyWindowContext(null);
            var windowFake = A.Fake<ILobbyWindow>();
            A.CallTo(() => _multiplayerClient.IsActive).Returns(true);
            var expectedGame = A.Fake<NetworkGame>();
            A.CallTo(() => _multiplayerClient.CurrentGame).Returns(expectedGame);
            A.CallTo(() => _uiFactory.InitializeEscMenuLobbyWindow(An<InitializeEscMenuLobbyWindowContext>.Ignored, An<bool>.Ignored, An<Action>.Ignored)).Returns(windowFake);

            // Act
            _multiplayer.InitializeEscMenuLobbyWindow(context);
            var actual = windowFake.NetworkGame();

            // Assert
            Assert.That(actual, Is.EqualTo(expectedGame));
        }

        [Test]
        public void InitializeMultiplayer_MultiplayerClientIsActive_LogsAndCallsDispose()
        {
            // Arrange
            var context = new InitializeMultiplayerContext(null, null);
            A.CallTo(() => _multiplayerClient.IsActive).Returns(true);

            // Act
            _multiplayer.InitializeMultiplayer(context);

            // Assert
            A.CallTo(() => _multiplayerClient.Dispose()).MustHaveHappenedOnceExactly();
        }
    }
}
