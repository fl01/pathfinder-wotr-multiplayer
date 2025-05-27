using System;
using Microsoft.Extensions.Logging;
using Owlcat.Runtime.UI.Controls.Button;
using UnityEngine;
using WOTRMultiplayer.Abstractions.UI;

namespace WOTRMultiplayer.UI.Menu.Items
{
    public abstract class MenuItemController
    {
        public const string SelectedGameObjectName = "SelectedImage";
        public const string HoverGameObjectName = "HoverImage";
        private readonly Microsoft.Extensions.Logging.ILogger _logger;

        private bool _isInitialized = false;
        private OwlcatButton Button => MenuItem.gameObject.GetComponent<OwlcatButton>();
        private GameObject _hoverImage;

        protected bool SetupLayout { get; set; } = true;
        protected GameObject ActiveImage { get; private set; }

        protected GameObject MenuItem { get; private set; }
        protected abstract GameObject MenuContent { get; }
        protected IMultiplayerMenuWindow Window { get; private set; }

        public bool IsActive => ActiveImage.activeSelf;

        public Action<object, EventArgs> OnClicked { get; set; }

        protected MenuItemController(Microsoft.Extensions.Logging.ILogger logger)
        {
            _logger = logger;
        }

        public void Initialize(IMultiplayerMenuWindow multiplayerMenuWindow, GameObject baseLayout, GameObject menuItem)
        {
            _logger.LogInformation("Trying to initialize");

            if (_isInitialized)
            {
                _logger.LogInformation("Already initialized");
                return;
            }

            MenuItem = menuItem;
            Window = multiplayerMenuWindow;
            _isInitialized = true;

            InitializeInternal(baseLayout);

            Button.OnHover.AddListener(OnHover);
            Button.OnLeftClick.AddListener(OnClickedInternal);
            ActiveImage = MenuItem.transform.Find(SelectedGameObjectName).gameObject;
            _hoverImage = MenuItem.transform.Find(HoverGameObjectName).gameObject;

            Deactivate();
        }

        public void Reset()
        {
            _logger.LogInformation("Resetting initialization");
            SetupLayout = true;
            _isInitialized = false;
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
    }
}
