using System;
using System.Collections.Generic;
using System.IO;
using Kingmaker.Localization;
using Kingmaker.PubSubSystem;
using Kingmaker.UI;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Owlcat.Runtime.UI.Controls.Button;
using TMPro;
using UnityEngine;
using WOTRMultiplayer.Abstractions;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.IO;
using WOTRMultiplayer.Abstractions.UI;
using WOTRMultiplayer.Abstractions.UI.Controllers;
using WOTRMultiplayer.Abstractions.UI.Controllers.Menu;
using WOTRMultiplayer.Abstractions.Unity;
using WOTRMultiplayer.Entities;
using WOTRMultiplayer.UI.Windows;

namespace WOTRMultiplayer.UI.Controllers
{
    public abstract class MenuItemControllerBase : IMultiplayerMenuItemController
    {
        public const string SelectedGameObjectName = "SelectedImage";
        public const string HoverGameObjectName = "HoverImage";
        private readonly Microsoft.Extensions.Logging.ILogger _logger;
        private bool _isInitialized = false;
        private OwlcatButton Button => MenuItem.gameObject.GetComponent<OwlcatButton>();
        private GameObject _hoverImage;
        private readonly IMultiplayerActor _multiplayerActor;

        protected string ServersFilePath => Path.Combine(GameInteractionService.GetPersistentDataPath(), "servers.json");

        protected ILobbyWindowController Lobby { get; private set; }

        protected IMainThreadAccessor MainThreadAccessor { get; private set; }

        protected IResourceProvider ResourceProvider { get; private set; }

        protected IFileSystemService FileSystemService { get; private set; }

        protected IUIFactory UIFactory { get; private set; }

        protected IGameInteractionService GameInteractionService { get; private set; }

        protected bool SetupLayout { get; set; } = true;

        protected GameObject ActiveImage { get; private set; }

        protected GameObject MenuItem { get; private set; }

        protected abstract GameObject MenuContent { get; }

        protected abstract LobbyWindowOwner Owner { get; }

        protected abstract GameObject ReadyButtonObject { get; }

        public bool IsActive => ActiveImage.activeSelf;

        public Action<object, EventArgs> OnClicked { get; set; }

        public Action<bool> OnChangeWindowVisibility { get; set; }

        public Action OnGameStarted { get; set; }

        protected MenuItemControllerBase(
            Microsoft.Extensions.Logging.ILogger logger,
            ILobbyWindowController lobbyWindowController,
            IMainThreadAccessor mainThreadAccessor,
            IResourceProvider resourceProvider,
            IFileSystemService fileSystemService,
            IUIFactory uiFactory,
            IGameInteractionService gameInteractionService,
            IMultiplayerActor multiplayerActor)
        {
            _logger = logger;
            _multiplayerActor = multiplayerActor;

            Lobby = lobbyWindowController;
            MainThreadAccessor = mainThreadAccessor;
            ResourceProvider = resourceProvider;
            FileSystemService = fileSystemService;
            UIFactory = uiFactory;
            GameInteractionService = gameInteractionService;
        }

        public virtual void Activate()
        {
            ActiveImage.SetActive(true);
            MenuContent.SetActive(true);

            _logger.LogInformation("Activated");
        }

        public virtual void Deactivate()
        {
            ActiveImage.SetActive(false);
            MenuContent.SetActive(false);

            _logger.LogInformation("Deactivated");
        }

        public ModalActionConfirmation GetDeactivationConfirmation()
        {
            return GetDeactivationConfirmationInternal();
        }

        public void Dispose()
        {
            _logger.LogInformation("Resetting controller");
            SetupLayout = true;
            _isInitialized = false;
            DisposeInternal();
        }

        public void Initialize(GameObject baseLayout, GameObject menuItem)
        {
            _logger.LogInformation("Trying to initialize");

            if (_isInitialized)
            {
                _logger.LogInformation("Already initialized");
                return;
            }

            MenuItem = menuItem;
            _isInitialized = true;

            InitializeInternal(baseLayout);

            Button.OnHover.AddListener(OnHover);
            Button.OnLeftClick.AddListener(OnClickedInternal);
            ActiveImage = MenuItem.transform.Find(SelectedGameObjectName).gameObject;
            _hoverImage = MenuItem.transform.Find(HoverGameObjectName).gameObject;
            ActiveImage.SetActive(false);
        }

        protected List<ExternalServer> GetExternalServers()
        {
            try
            {
                var content = FileSystemService.GetFileContent(ServersFilePath);
                if (content == null)
                {
                    _logger.LogInformation("Creating default configuration for external servers...");
                    var defaultServers = new List<ExternalServer>
                    {
                        new() { Url = "https://eu.wotr.arva.moe", GameHubPath = "hubs/game", Name = "Europe", Prefix = "EU"  }
                    };
                    var json = JsonConvert.SerializeObject(defaultServers, Formatting.Indented);
                    try
                    {
                        FileSystemService.WriteFile(ServersFilePath, json);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Unable to save external servers");
                        var message = string.Format(new LocalizedString { Key = WellKnownKeys.MultiplayerWindow.ExternalServers.Errors.UnableToSave.Key }, ServersFilePath);
                        EventBus.RaiseEvent<IMessageModalUIHandler>(x => x.HandleOpen(message));
                    }
                    return defaultServers;
                }

                var storedServers = JsonConvert.DeserializeObject<List<ExternalServer>>(content);
                return storedServers;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to read external servers");
                var message = string.Format(new LocalizedString { Key = WellKnownKeys.MultiplayerWindow.ExternalServers.Errors.UnableToLoad.Key }, ex.Message);
                EventBus.RaiseEvent<IMessageModalUIHandler>(x => x.HandleOpen(message));
                return [];
            }
        }

        protected void OnMultiplayerOnGameStarted()
        {
            Lobby.UpdateLoadingProgress(null);
        }

        protected void OnMultiplayerSaveGameTransferProgressChanged(Dictionary<long, float> progress)
        {
            Lobby.UpdateLoadingProgress(progress);
        }

        protected void OnMultiplayerNewGameSequenceStarted(bool isCancelled)
        {
            MainThreadAccessor.Post(() =>
            {
                if (isCancelled)
                {
                    ToggleReadyButton(false);
                }

                OnChangeWindowVisibility?.Invoke(!isCancelled);
            });
        }

        protected void OnReadyButtonClicked()
        {
            var isReady = _multiplayerActor.ReadyChanged();
            ToggleReadyButton(isReady);
        }

        protected void ToggleReadyButton(bool isReady)
        {
            var label = isReady ? new LocalizedString { Key = WellKnownKeys.MultiplayerWindow.ReadyButton.NotReadyText.Key }
                : new LocalizedString { Key = WellKnownKeys.MultiplayerWindow.ReadyButton.ReadyText.Key };
            ReadyButtonObject.GetComponentInChildren<TextMeshProUGUI>().SetText(label);
        }

        protected virtual void DisposeInternal()
        {
            Button.OnHover.RemoveAllListeners();
            Button.OnLeftClick.RemoveAllListeners();
            ActiveImage = null;
            _hoverImage = null;

            Lobby.ResetOwnerContent(Owner);
        }

        protected virtual void InitializeInternal(GameObject baseLayout)
        {
        }

        private void OnHover(bool state)
        {
            _hoverImage.SetActive(state);
        }

        protected void OnEveryoneIsReady()
        {
            UISoundController.Instance.Play(UISoundType.GlobalMapReTokenAppear);
        }

        protected virtual ModalActionConfirmation GetDeactivationConfirmationInternal()
        {
            return null;
        }

        private void OnClickedInternal()
        {
            OnClicked?.Invoke(this, EventArgs.Empty);
        }
    }
}
