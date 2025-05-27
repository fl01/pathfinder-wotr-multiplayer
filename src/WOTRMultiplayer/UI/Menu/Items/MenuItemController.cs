using System;
using Owlcat.Runtime.UI.Controls.Button;
using Serilog;
using UnityEngine;

namespace WOTRMultiplayer.UI.Menu.Items
{
    public abstract class MenuItemController
    {
        public const string SelectedGameObjectName = "SelectedImage";
        public const string HoverGameObjectName = "HoverImage";

        private bool _isInitialized = false;
        private OwlcatButton Button => MenuItem.gameObject.GetComponent<OwlcatButton>();
        private GameObject _hoverImage;

        public GameObject MenuItem { get; private set; }
        public abstract GameObject MenuContent { get; }
        protected GameObject ActiveImage { get; private set; }
        protected MultiplayerWindow Window { get; private set; }

        public bool IsActive => ActiveImage.activeSelf;

        public event EventHandler OnClicked;

        public MenuItemController(MultiplayerWindow multiplayerWindow, GameObject menuItem)
        {
            Log.Logger.Information("Creating {controllerTypeName}. Type={type}", nameof(MenuItemController), GetType().Name);

            MenuItem = menuItem;
            Window = multiplayerWindow;
        }

        public void Initialize(GameObject baseLayout)
        {
            if (_isInitialized)
            {
                return;
            }

            _isInitialized = true;

            InitializeInternal(baseLayout);

            Button.OnHover.AddListener(OnHover);
            Button.OnLeftClick.AddListener(OnClickedInternal);
            ActiveImage = MenuItem.transform.Find(SelectedGameObjectName).gameObject;
            _hoverImage = MenuItem.transform.Find(HoverGameObjectName).gameObject;

            Deactivate();
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

            Log.Logger.Information("Activated {controllerTypeName}. Type={type}", nameof(MenuItemController), GetType().Name);
        }

        public virtual void Deactivate()
        {
            ActiveImage.SetActive(false);
            MenuContent.SetActive(false);

            Log.Logger.Information("Deactivated {controllerTypeName}. Type={type}", nameof(MenuItemController), GetType().Name);

        }

        private void OnClickedInternal()
        {
            OnClicked?.Invoke(this, EventArgs.Empty);
        }
    }
}
