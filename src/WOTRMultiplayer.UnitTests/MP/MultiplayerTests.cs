using System;
using System.Collections.Generic;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.MP;
using WOTRMultiplayer.Abstractions.UI;
using WOTRMultiplayer.Abstractions.UI.Controllers;
using WOTRMultiplayer.Abstractions.UI.Windows;
using WOTRMultiplayer.MP;
using WOTRMultiplayer.MP.Entities;
using WOTRMultiplayer.UI.Menu;

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
        private IDiceRollStorage _rollStorage;
        private IGameInteractionService _gameInteractionService;

        [SetUp]
        public void SetUp()
        {
            _logger = A.Fake<ILogger<Multiplayer>>();
            _uiFactory = A.Fake<IUIFactory>();
            _lobbyWindowController = A.Fake<ILobbyWindowController>();
            _multiplayerHost = A.Fake<IMultiplayerHost>();
            _multiplayerClient = A.Fake<IMultiplayerClient>();
            _rollStorage = A.Fake<IDiceRollStorage>();
            _gameInteractionService = A.Fake<IGameInteractionService>();

            _multiplayer = new Multiplayer(
                _logger,
                _uiFactory,
                _lobbyWindowController,
                _multiplayerHost,
                _multiplayerClient,
                _rollStorage,
                _gameInteractionService);
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

        [Test]
        public void InitializeEscMenuLobbyWindow_HostIsActive_CallsFactory()
        {
            // Arrange
            var context = new InitializeEscMenuLobbyWindowContext(null);
            A.CallTo(() => _multiplayerHost.IsActive).Returns(true);

            // Act
            _multiplayer.InitializeEscMenuLobbyWindow(context);

            // Assert
            A.CallTo(() => _uiFactory.InitializeEscMenuLobbyWindow(context, true, An<Action>.Ignored)).MustHaveHappenedOnceExactly();
        }

        [Test]
        public void InitializeEscMenuLobbyWindow_ClientIsActive_CallsFactory()
        {
            // Arrange
            var context = new InitializeEscMenuLobbyWindowContext(null);
            A.CallTo(() => _multiplayerClient.IsActive).Returns(true);

            // Act
            _multiplayer.InitializeEscMenuLobbyWindow(context);

            // Assert
            A.CallTo(() => _uiFactory.InitializeEscMenuLobbyWindow(context, false, An<Action>.Ignored)).MustHaveHappenedOnceExactly();
        }

        [Test]
        public void InitializeEscMenuLobbyWindow_NoActiveMultiplayerActors_DidnotCallFactory()
        {
            // Arrange
            var context = new InitializeEscMenuLobbyWindowContext(null);
            A.CallTo(() => _multiplayerClient.IsActive).Returns(false);
            A.CallTo(() => _multiplayerHost.IsActive).Returns(false);

            // Act
            _multiplayer.InitializeEscMenuLobbyWindow(context);

            // Assert
            A.CallTo(() => _uiFactory.InitializeEscMenuLobbyWindow(An<InitializeEscMenuLobbyWindowContext>.Ignored, An<bool>.Ignored, An<Action>.Ignored)).MustNotHaveHappened();
        }


        [Test]
        public void InitializeEscMenuLobbyWindow_ConfiguresLobbyWindow()
        {
            // Arrange
            var context = new InitializeEscMenuLobbyWindowContext(null);
            var windowFake = A.Fake<ILobbyWindow>();
            A.CallTo(() => _multiplayerClient.IsActive).Returns(true);
            A.CallTo(() => _uiFactory.InitializeEscMenuLobbyWindow(An<InitializeEscMenuLobbyWindowContext>.Ignored, An<bool>.Ignored, An<Action>.Ignored)).Returns(windowFake);

            // Act
            _multiplayer.InitializeEscMenuLobbyWindow(context);

            // Assert
            A.CallTo(() => windowFake.AssignLobbyController(_lobbyWindowController)).MustHaveHappenedOnceExactly();
            Assert.That(windowFake.GetGameConnectivity, Is.Not.Null);
            Assert.That(windowFake.GetCharacters, Is.Not.Null);
            Assert.That(windowFake.GetPlayers, Is.Not.Null);
        }

        [Test]
        public void InitializeEscMenuLobbyWindow_HostIsActive_GameConnectivityIsTakenFromHost()
        {
            // Arrange
            var context = new InitializeEscMenuLobbyWindowContext(null);
            var windowFake = A.Fake<ILobbyWindow>();
            A.CallTo(() => _multiplayerHost.IsActive).Returns(true);
            var expectedConnectivity = A.Fake<NetworkGameConnectivity>();
            A.CallTo(() => _multiplayerHost.GetGameConnectivity()).Returns(expectedConnectivity);
            A.CallTo(() => _uiFactory.InitializeEscMenuLobbyWindow(An<InitializeEscMenuLobbyWindowContext>.Ignored, An<bool>.Ignored, An<Action>.Ignored)).Returns(windowFake);

            // Act
            _multiplayer.InitializeEscMenuLobbyWindow(context);
            var actual = windowFake.GetGameConnectivity();

            // Assert
            Assert.That(actual, Is.EqualTo(expectedConnectivity));
        }

        [Test]
        public void InitializeEscMenuLobbyWindow_HostIsActive_PlayerInfoIsTakenFromHost()
        {
            // Arrange
            var context = new InitializeEscMenuLobbyWindowContext(null);
            var windowFake = A.Fake<ILobbyWindow>();
            A.CallTo(() => _multiplayerHost.IsActive).Returns(true);
            var expectedPlayers = A.Fake<List<NetworkPlayer>>();
            A.CallTo(() => _multiplayerHost.GetPlayers()).Returns(expectedPlayers);
            A.CallTo(() => _uiFactory.InitializeEscMenuLobbyWindow(An<InitializeEscMenuLobbyWindowContext>.Ignored, An<bool>.Ignored, An<Action>.Ignored)).Returns(windowFake);

            // Act
            _multiplayer.InitializeEscMenuLobbyWindow(context);
            var actual = windowFake.GetPlayers();

            // Assert
            Assert.That(actual, Is.EqualTo(expectedPlayers));
        }

        [Test]
        public void InitializeEscMenuLobbyWindow_HostIsActive_CharactersInfoIsTakenFromHost()
        {
            // Arrange
            var context = new InitializeEscMenuLobbyWindowContext(null);
            var windowFake = A.Fake<ILobbyWindow>();
            A.CallTo(() => _multiplayerHost.IsActive).Returns(true);
            var expectedCharacters = A.Fake<List<NetworkCharacterOwnership>>();
            A.CallTo(() => _multiplayerHost.GetCharacters()).Returns(expectedCharacters);
            A.CallTo(() => _uiFactory.InitializeEscMenuLobbyWindow(An<InitializeEscMenuLobbyWindowContext>.Ignored, An<bool>.Ignored, An<Action>.Ignored)).Returns(windowFake);

            // Act
            _multiplayer.InitializeEscMenuLobbyWindow(context);
            var actual = windowFake.GetCharacters();

            // Assert
            Assert.That(actual, Is.EqualTo(expectedCharacters));
        }


        [Test]
        public void InitializeEscMenuLobbyWindow_ClientIsActive_GameConnectivityIsTakenFromHost()
        {
            // Arrange
            var context = new InitializeEscMenuLobbyWindowContext(null);
            var windowFake = A.Fake<ILobbyWindow>();
            A.CallTo(() => _multiplayerClient.IsActive).Returns(true);
            var expectedConnectivity = A.Fake<NetworkGameConnectivity>();
            A.CallTo(() => _multiplayerClient.GetGameConnectivity()).Returns(expectedConnectivity);
            A.CallTo(() => _uiFactory.InitializeEscMenuLobbyWindow(An<InitializeEscMenuLobbyWindowContext>.Ignored, An<bool>.Ignored, An<Action>.Ignored)).Returns(windowFake);

            // Act
            _multiplayer.InitializeEscMenuLobbyWindow(context);
            var actual = windowFake.GetGameConnectivity();

            // Assert
            Assert.That(actual, Is.EqualTo(expectedConnectivity));
        }

        [Test]
        public void InitializeEscMenuLobbyWindow_ClientIsActive_PlayerInfoIsTakenFromHost()
        {
            // Arrange
            var context = new InitializeEscMenuLobbyWindowContext(null);
            var windowFake = A.Fake<ILobbyWindow>();
            A.CallTo(() => _multiplayerClient.IsActive).Returns(true);
            var expectedPlayers = A.Fake<List<NetworkPlayer>>();
            A.CallTo(() => _multiplayerClient.GetPlayers()).Returns(expectedPlayers);
            A.CallTo(() => _uiFactory.InitializeEscMenuLobbyWindow(An<InitializeEscMenuLobbyWindowContext>.Ignored, An<bool>.Ignored, An<Action>.Ignored)).Returns(windowFake);

            // Act
            _multiplayer.InitializeEscMenuLobbyWindow(context);
            var actual = windowFake.GetPlayers();

            // Assert
            Assert.That(actual, Is.EqualTo(expectedPlayers));
        }

        [Test]
        public void InitializeEscMenuLobbyWindow_ClientIsActive_CharactersInfoIsTakenFromHost()
        {
            // Arrange
            var context = new InitializeEscMenuLobbyWindowContext(null);
            var windowFake = A.Fake<ILobbyWindow>();
            A.CallTo(() => _multiplayerClient.IsActive).Returns(true);
            var expectedCharacters = A.Fake<List<NetworkCharacterOwnership>>();
            A.CallTo(() => _multiplayerClient.GetCharacters()).Returns(expectedCharacters);
            A.CallTo(() => _uiFactory.InitializeEscMenuLobbyWindow(An<InitializeEscMenuLobbyWindowContext>.Ignored, An<bool>.Ignored, An<Action>.Ignored)).Returns(windowFake);

            // Act
            _multiplayer.InitializeEscMenuLobbyWindow(context);
            var actual = windowFake.GetCharacters();

            // Assert
            Assert.That(actual, Is.EqualTo(expectedCharacters));
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
