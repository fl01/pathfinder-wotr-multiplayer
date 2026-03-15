using System;
using System.Collections.Generic;
using Kingmaker;
using Kingmaker.UI.ServiceWindow;
using Microsoft.Extensions.Logging;
using UnityEngine;
using WOTRMultiplayer.Abstractions.UI.Controllers;
using WOTRMultiplayer.Abstractions.UI.Windows;
using WOTRMultiplayer.Entities;

namespace WOTRMultiplayer.UI.Windows
{
    public class LobbyWindow : UIWindow, ILobbyWindow
    {
        private ILogger<LobbyWindow> _logger;
        private ILobbyWindowController _lobbyWindowController;
        private Action _onClose;
        private IDisposable _escSubscription;

        public Func<NetworkGameConnectivity> GetGameConnectivity { get; set; }

        public Func<List<NetworkPlayer>> GetPlayers { get; set; }

        public Func<List<NetworkCharacter>> GetCharacters { get; set; }

        public Func<bool> GetIsHost { get; set; }

        public GameObject Initiator { get; private set; }

        public bool IsVisible => base.IsShow;

        public LobbyWindow WithLogger(ILogger<LobbyWindow> logger)
        {
            _logger = logger;
            return this;
        }

        public ILobbyWindow WithController(ILobbyWindowController controller)
        {
            _lobbyWindowController = controller;
            return this;
        }

        public ILobbyWindow WithCloseHandler(Action onClose)
        {
            _onClose = onClose;
            return this;
        }

        public ILobbyWindow WithInitiator(GameObject initiator)
        {
            Initiator = initiator;
            return this;
        }

        public ILobbyWindow Initialize(LobbyWindowOwner lobbyWindowOwner)
        {
            _lobbyWindowController.InitializeContent(lobbyWindowOwner, transform);
            return this;
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
                _lobbyWindowController.UpdateCharacters(characters, GetIsHost());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to update data within the window");
            }

            _logger.LogInformation("Subscribing for esc button click");
            _escSubscription = Game.Instance.UI.EscManager.Subscribe(Close);
        }

        public override void Dispose()
        {
            _logger.LogInformation("Dispose");
            _escSubscription?.Dispose();
            _escSubscription = null;
            base.Dispose();
        }

        public void Close()
        {
            _logger.LogInformation("Close lobby window");
            _lobbyWindowController?.ResetData();
            Show(false);
            _onClose?.Invoke();
        }

        public void Show()
        {
            Show(true);
        }
    }
}
