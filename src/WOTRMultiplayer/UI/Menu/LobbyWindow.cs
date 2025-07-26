using System;
using System.Collections.Generic;
using Kingmaker;
using Kingmaker.UI.ServiceWindow;
using Microsoft.Extensions.Logging;
using UnityEngine;
using WOTRMultiplayer.Abstractions.UI.Controllers;
using WOTRMultiplayer.Abstractions.UI.Windows;
using WOTRMultiplayer.MP.Entities;

namespace WOTRMultiplayer.UI.Menu
{
    public class LobbyWindow : UIWindow, ILobbyWindow
    {
        private ILogger<LobbyWindow> _logger;
        private ILobbyWindowController _lobbyWindowController;

        public Func<NetworkGameConnectivity> GetGameConnectivity { get; set; }
        public Func<List<NetworkPlayer>> GetPlayers { get; set; }
        public Func<List<NetworkCharacterOwnership>> GetCharacters { get; set; }

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
                var connectivity = GetGameConnectivity();
                _lobbyWindowController.UpdateServerInfo(connectivity);
                var players = GetPlayers();
                _lobbyWindowController.UpdatePlayers(players);
                var characters = GetCharacters();
                _lobbyWindowController.UpdateCharacters(characters);

                for (int i = 0; i < characters.Count; i++)
                {
                    var character = characters[i];
                    var playerIndex = players.IndexOf(character.Owner);
                    _lobbyWindowController.UpdateCharacterOwnerDropdown(i, playerIndex);
                }
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
            base.Dispose();
        }

        private void Close()
        {
            _logger.LogInformation("Close lobby window");
            _lobbyWindowController?.ResetData();
            Show(false);
        }
    }
}
