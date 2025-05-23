using System;
using System.Collections.Generic;
using DG.Tweening;
using Kingmaker.UI.FullScreenUITypes;
using Kingmaker.UI.MVVM._PCView.SaveLoad;
using Kingmaker.UI.ServiceWindow;
using Owlcat.Runtime.UI.Controls.Button;
using UnityEngine;
using WOTRMultiplayer.Extensions;
using WOTRMultiplayer.UI.Menu.Items;

namespace WOTRMultiplayer.UI.Menu
{
    public class MultiplayerWindow : FullScreenTabsWindow
    {
        private const string BaseLayoutName = "MultiplayerScreen";

        private const string SeparatorGameObjectName = "Separator";

        private const string MultiplayerMenuItemsObjectName = "MultiplayerMenuItems";
        private const string HostMenuItemContentObjectName = "HostMenuItemContent";
        private const string JoinMenuItemContentObjectName = "JoinMenuItemContent";
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
                Logging.Logger.Warning("Trying to initialize already initialized window");
                return;
            }

            Main.Multiplayer.ElementFactory.StoreDefaultGameObject(this.gameObject.transform.Find("Black").gameObject);

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
            Logging.Logger.Info($"Show/Hide {nameof(MultiplayerWindow)}. State={state}");
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
            baseLayout.name = BaseLayoutName;
            var (hostItemContent, joinItemContent) = SetupMenuItemsContentLayout(baseLayout);
            SetupMenuItemsLayout(baseLayout, hostItemContent, joinItemContent);
        }

        private (GameObject hostItemContent, GameObject joinItemContent) SetupMenuItemsContentLayout(GameObject baseLayout)
        {
            var hostItemContent = Instantiate(baseLayout, baseLayout.transform);
            hostItemContent.name = HostMenuItemContentObjectName;
            hostItemContent.CleanupAllChildren();
            SetupLoadSaveGamesLayout(hostItemContent);
            SetupLobbyInfo(baseLayout, hostItemContent);

            var joinItemContent = Instantiate(baseLayout, baseLayout.transform);
            joinItemContent.name = JoinMenuItemContentObjectName;
            joinItemContent.CleanupAllChildren();
            return (hostItemContent, joinItemContent);
        }

        private void SetupLobbyInfo(GameObject baseLayout, GameObject hostItemContent)
        {
            var saveLoadView = hostItemContent.transform.GetChild(0);
            var screen = saveLoadView.gameObject.transform.Find("SaveLoadScreen");
            var container = screen.Find("SaveLoadDetails");

            var lobbyWindowObject = Instantiate(baseLayout, container.transform);
            lobbyWindowObject.name = "MultiplayerLobby";
            lobbyWindowObject.CleanupAllChildren();
            var title = container.Find("Title");
            var lobbyWindowObjectPosition = new Vector3(title.position.x, lobbyWindowObject.transform.position.y * 1.1f, lobbyWindowObject.transform.position.z);
            lobbyWindowObject.transform.SetPositionAndRotation(lobbyWindowObjectPosition, lobbyWindowObject.transform.rotation);
            var parentContainerRect = container.GetComponent<RectTransform>();
            var lobbyWindowObjectRect = lobbyWindowObject.GetComponent<RectTransform>();
            lobbyWindowObjectRect.sizeDelta = new Vector2(parentContainerRect.sizeDelta.x * 0.9f, parentContainerRect.sizeDelta.y * 0.72f);

            var lobbyContent = Main.Multiplayer.ElementFactory.CreateLobbyWindowContent(lobbyWindowObject.transform);
        }

        private void SetupLoadSaveGamesLayout(GameObject hostItemContent)
        {
            SaveLoadPCView saveLoad = Main.Multiplayer.ElementFactory.CreateSaveLoadPCView(hostItemContent.transform);
            saveLoad.Initialize();
        }

        private void SetupMenuItemsLayout(GameObject baseLayout, GameObject hostItemContent, GameObject joinItemContent)
        {
            baseLayout.name = MultiplayerMenuItemsObjectName;
            baseLayout.CleanupAllChildren(
                x => x.name != HostMenuItemContentObjectName && x.name != JoinMenuItemContentObjectName && x.name != MenuOverridesObjectName);

            var baseMenuItem = SetupBaseMenuItem(baseLayout);
            _hostMenuController = SetupMenuController<HostMenuItemController>(MultiplayerMenuItemType.Host, Screen.width * 0.33f, hostItemContent, baseMenuItem, baseLayout.transform);
            _joinMenuController = SetupMenuController<JoinMenuItemController>(MultiplayerMenuItemType.Join, Screen.width * 0.66f, joinItemContent, baseMenuItem, baseLayout.transform);
            DestroyImmediate(baseMenuItem);

            _hostMenuController.OnClicked += OnHostMenuItemClicked;
            _joinMenuController.OnClicked += OnJoinMenuItemClicked;
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
            var baseItem = baseLayoutObject.transform.GetChild(0).gameObject;

            DestroyImmediate(baseItem.GetComponent<OwlcatMultiButton>());
            baseItem.AddComponent<OwlcatButton>();

            var endSeparator = baseItem.transform.Find(SeparatorGameObjectName);
            DestroyImmediate(endSeparator.gameObject);

            return baseItem;
        }

        private T SetupMenuController<T>(
            MultiplayerMenuItemType menuItemType,
            float positionX,
            GameObject menuContent,
            GameObject baseMenuItem,
            Transform parent)
            where T : MenuItemController
        {
            var menuItem = Instantiate(baseMenuItem, parent);
            var position = new Vector3(positionX, menuItem.transform.position.y, menuItem.transform.position.z);
            menuItem.transform.SetPositionAndRotation(position, menuItem.transform.rotation);

            MenuItemController controller = menuItemType switch
            {
                MultiplayerMenuItemType.Host => new HostMenuItemController(this, menuItem, menuContent),
                MultiplayerMenuItemType.Join => new JoinMenuItemController(this, menuItem, menuContent),
                _ => throw new InvalidOperationException($"Unknown menuItemType. Value={menuItemType}")
            };

            controller.Initialize();

            return (T)controller;
        }

        private enum MultiplayerMenuItemType
        {
            Host,
            Join
        }
    }
}
