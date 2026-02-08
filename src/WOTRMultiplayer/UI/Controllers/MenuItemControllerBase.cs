using System;
using Kingmaker.Localization;
using Kingmaker.UI;
using Microsoft.Extensions.Logging;
using Owlcat.Runtime.UI.Controls.Button;
using TMPro;
using UnityEngine;
using WOTRMultiplayer.Abstractions;
using WOTRMultiplayer.Abstractions.UI;
using WOTRMultiplayer.Abstractions.UI.Controllers;
using WOTRMultiplayer.Abstractions.UI.Controllers.Menu;
using WOTRMultiplayer.Abstractions.Unity;
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

        protected ILobbyWindowController Lobby { get; private set; }

        protected IMainThreadAccessor MainThreadAccessor { get; private set; }

        protected IResourceProvider ResourceProvider { get; private set; }


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
            IMultiplayerActor multiplayerActor)
        {
            _logger = logger;
            Lobby = lobbyWindowController;
            MainThreadAccessor = mainThreadAccessor;
            ResourceProvider = resourceProvider;
            _multiplayerActor = multiplayerActor;
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
