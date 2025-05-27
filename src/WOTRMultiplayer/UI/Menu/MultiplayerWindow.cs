using System;
using System.Collections.Generic;
using DG.Tweening;
using Kingmaker.UI.FullScreenUITypes;
using Kingmaker.UI.ServiceWindow;
using Owlcat.Runtime.UI.Controls.Button;
using Serilog;
using UnityEngine;
using WOTRMultiplayer.Extensions;
using WOTRMultiplayer.UI.Menu.Items;

namespace WOTRMultiplayer.UI.Menu
{
    public class MultiplayerWindow : FullScreenTabsWindow
    {
        private const string BaseLayoutName = "MultiplayerScreen";
        private const string SeparatorGameObjectName = "Separator";
        private const string MenuOverridesObjectName = "MenuEntity_NoOverrides";

        public override FullScreenUIType ActiveFullScreenUIType => (FullScreenUIType)555555;

        private List<DOTweenAnimation> _animations = [];

        private bool _isInitialized = false;

        private HostMenuItemController _hostMenuController;
        private JoinMenuItemController _joinMenuController;

        public MultiplayerWindow()
        {
            // I assume this should be used to display menu items content,
            // but I have no idea how to make it work, so have to rely on my own `MenuItemController.MenuContent` implementation
            SubWindowsList = [];
        }

        public override void Initialize()
        {
            if (_isInitialized)
            {
                Log.Logger.Warning("Trying to initialize already initialized window");
                return;
            }

            Main.Multiplayer.Factory.StoreDefaultGameObject(this.gameObject.transform.Find("Black").gameObject);

            SetupLayout();

            _isInitialized = true;
            base.Initialize();
            IsAnimated = true;
            var canvas = GetComponent<CanvasGroup>();
            canvas.alpha = 0f;
            _animations = [.. GetComponents<DOTweenAnimation>()];
            var closeButton = GetComponentInChildren<OwlcatButton>();
            closeButton.OnLeftClick.AddListener(OnCloseClicked);
        }

        public override void AppearAnimation()
        {
            base.AppearAnimation();
            gameObject.SetActive(true);
            foreach (var animation in _animations)
            {
                animation.DOPlayForward();
            }
        }

        public override void DisappearAnimation()
        {
            base.DisappearAnimation();
            foreach (var animation in _animations)
            {
                animation.DOPlayBackwards();
            }
            gameObject.SetActive(false);
        }

        public override void OnHide()
        {
            _joinMenuController.Deactivate();
            _hostMenuController.Deactivate();
            StopAllCoroutines();
            base.OnHide();
        }

        public override void Show(bool state)
        {
            Log.Logger.Information("Show/Hide {windowTypeName}, State={state}", nameof(MultiplayerWindow), state);
            if (state)
            {
                _hostMenuController.Activate();
            }

            base.Show(state);
        }

        public void OnCloseClicked()
        {
            _hostMenuController?.Deactivate();
            _joinMenuController?.Deactivate();
            OnButtonClose();
        }

        private void SetupLayout()
        {
            var baseLayout = transform.Find("CreditsScreen")?.gameObject;
            var baseMenuItem = SetupBaseMenuItem(baseLayout);
            var hostMenuItem = CreateMenuItem(Screen.width * 0.33f, baseMenuItem, baseLayout.transform);
            _hostMenuController = new HostMenuItemController(this, hostMenuItem);
            _hostMenuController.Initialize(baseLayout);

            var joinMenuItem = CreateMenuItem(Screen.width * 0.66f, baseMenuItem, baseLayout.transform);
            _joinMenuController = new JoinMenuItemController(this, joinMenuItem);
            _joinMenuController.Initialize(baseLayout);
            DestroyImmediate(baseMenuItem);

            _hostMenuController.OnClicked += OnHostMenuItemClicked;
            _joinMenuController.OnClicked += OnJoinMenuItemClicked;
        }

        private GameObject CreateMenuItem(float positionX, GameObject objToCopy, Transform parent)
        {
            var menuItem = Instantiate(objToCopy, parent);
            var position = new Vector3(positionX, menuItem.transform.position.y, menuItem.transform.position.z);
            menuItem.transform.SetPositionAndRotation(position, menuItem.transform.rotation);
            return menuItem;
        }

        private void OnHostMenuItemClicked(object sender, EventArgs e)
        {
            _hostMenuController.Activate();
            _joinMenuController.Deactivate();
        }

        private void OnJoinMenuItemClicked(object sender, EventArgs e)
        {
            _hostMenuController.Deactivate();
            _joinMenuController.Activate();
        }

        private GameObject SetupBaseMenuItem(GameObject baseLayoutObject)
        {
            baseLayoutObject.name = BaseLayoutName;
            baseLayoutObject.gameObject.CleanupAllChildren(x => x.name != MenuOverridesObjectName);
            var baseItem = baseLayoutObject.transform.Find(MenuOverridesObjectName).gameObject;

            DestroyImmediate(baseItem.GetComponent<OwlcatMultiButton>());
            baseItem.AddComponent<OwlcatButton>();

            var endSeparator = baseItem.transform.Find(SeparatorGameObjectName);
            DestroyImmediate(endSeparator.gameObject);

            return baseItem;
        }
    }
}
