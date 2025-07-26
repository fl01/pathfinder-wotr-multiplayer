using System;
using Microsoft.Extensions.Logging;
using Owlcat.Runtime.UI.Controls.Button;
using UnityEngine;
using WOTRMultiplayer.Abstractions.GameInteraction;
using WOTRMultiplayer.Abstractions.UI.Controllers;
using WOTRMultiplayer.Abstractions.UI.Controllers.Menu;
using WOTRMultiplayer.Abstractions.Unity;
using WOTRMultiplayer.UI.Menu;

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

        protected ILobbyWindowController Lobby { get; private set; }

        protected IMainThreadAccessor MainThreadAccessor { get; private set; }

        protected IGameInteractionService GameInteraction { get; private set; }

        protected bool SetupLayout { get; set; } = true;
        protected GameObject ActiveImage { get; private set; }

        protected GameObject MenuItem { get; private set; }
        protected abstract GameObject MenuContent { get; }

        protected abstract LobbyWindowOwner Owner { get; }

        public bool IsActive => ActiveImage.activeSelf;

        public Action<object, EventArgs> OnClicked { get; set; }

        public Action OnGameStarted { get; set; }

        protected MenuItemControllerBase(
            Microsoft.Extensions.Logging.ILogger logger,
            ILobbyWindowController lobbyWindowController,
            IMainThreadAccessor mainThreadAccessor,
            IGameInteractionService gameInteractionService)
        {
            _logger = logger;
            Lobby = lobbyWindowController;
            MainThreadAccessor = mainThreadAccessor;
            GameInteraction = gameInteractionService;
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

        private void OnClickedInternal()
        {
            OnClicked?.Invoke(this, EventArgs.Empty);
        }

        protected virtual ModalActionConfirmation GetDeactivationConfirmationInternal()
        {
            return null;
        }

        public ModalActionConfirmation GetDeactivationConfirmation()
        {
            return GetDeactivationConfirmationInternal();
        }
    }
}
