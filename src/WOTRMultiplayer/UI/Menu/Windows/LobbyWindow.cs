using System;
using Kingmaker;
using Kingmaker.UI.ServiceWindow;
using Microsoft.Extensions.Logging;
using UnityEngine;
using WOTRMultiplayer.Abstractions.UI.Controllers;
using WOTRMultiplayer.Abstractions.UI.Windows;
using WOTRMultiplayer.MP.Entities;
using WOTRMultiplayer.UI.Lobby;

namespace WOTRMultiplayer.UI.Menu.Windows
{
    public class LobbyWindow : UIWindow, ILobbyWindow
    {
        private ILogger<LobbyWindow> _logger;
        private ILobbyWindowController _lobbyWindowController;

        public Func<NetworkGame> NetworkGame { get; set; }

        public GameObject MenuItem { get; set; }

        public void SetLogger(ILogger<LobbyWindow> logger)
        {
            _logger = logger;
        }

        public void AssignLobbyController(ILobbyWindowController controller)
        {
            _lobbyWindowController = controller;
        }

        public override void OnHide()
        {
            _logger.LogInformation("OnHide");
            Game.Instance.UI.EscManager.Unsubscribe(Close);
        }

        public override void OnShow()
        {
            _logger.LogInformation("OnShow");
            try
            {
                _lobbyWindowController.SetActiveOwner(LobbyWindowOwner.EscMenu);

                _logger.LogInformation("Updaing lobby info");
                var game = NetworkGame();
                _lobbyWindowController.UpdateServerInfo(game.Endpoint.ToString());
                _lobbyWindowController.UpdatePlayers(game.Players);
                _lobbyWindowController.UpdatePortraits(game.Portraits);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to update data within the window");
            }

            _logger.LogInformation("Subscribing for esc button click");
            Game.Instance.UI.EscManager.Subscribe(Close);
        }

        public override void Dispose()
        {
            _logger.LogInformation("Dispose");

            NetworkGame = null;
            base.Dispose();
        }

        private void Close()
        {
            _logger.LogInformation("Close lobby window");
            _lobbyWindowController?.ResetData();
            this.Show(false);
        }
    }
}
