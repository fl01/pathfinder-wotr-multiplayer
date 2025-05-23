using System;
using System.Collections.Generic;
using DG.Tweening;
using Kingmaker.UI;
using Kingmaker.UI.Common;
using Kingmaker.UI.FullScreenUITypes;
using Kingmaker.UI.MVVM;
using Kingmaker.UI.MVVM._PCView.Common;
using Kingmaker.UI.MVVM._PCView.SaveLoad;
using Kingmaker.UI.MVVM._PCView.Settings.Entities;
using Kingmaker.UI.ServiceWindow;
using Owlcat.Runtime.UI.Controls.Button;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WOTRMultiplayer.Extensions;
using WOTRMultiplayer.UI.Menu.Items;

namespace WOTRMultiplayer.UI.Menu
{
    public class MultiplayerWindow : FullScreenTabsWindow
    {
        private static SaveLoadPCView _cachedSaveLoadPCView;

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
            lobbyWindowObject.transform.SetSiblingIndex(2);
            lobbyWindowObject.name = "MultiplayerLobby";
            lobbyWindowObject.CleanupAllChildren();
            var title = container.Find("Title");
            var lobbyWindowObjectPosition = new Vector3(title.position.x, lobbyWindowObject.transform.position.y * 1.1f, lobbyWindowObject.transform.position.z);
            lobbyWindowObject.transform.SetPositionAndRotation(lobbyWindowObjectPosition, lobbyWindowObject.transform.rotation);
            var parentContainerRect = container.GetComponent<RectTransform>();
            var lobbyWindowObjectRect = lobbyWindowObject.GetComponent<RectTransform>();
            lobbyWindowObjectRect.sizeDelta = new Vector2(parentContainerRect.sizeDelta.x * 0.9f, parentContainerRect.sizeDelta.y * 0.72f);

            var lobbyContent = InstantiateDefaultGameObject(lobbyWindowObjectRect.transform);
            DestroyImmediate(lobbyContent.GetComponent<UnityEngine.UI.Image>());
            lobbyContent.CleanupAllChildren();
            var background = InstantiateDefaultGameObject(lobbyContent.transform);
            DestroyImmediate(background.GetComponent<UnityEngine.UI.Image>());
            background.name = "Background";
            var saveList = screen.Find("SaveSlotCollectionPlace").Find("SaveSlotVirtualCollectionView");
            Instantiate(saveList.Find("decoration").gameObject, background.transform);
            Instantiate(saveList.Find("Decoration").gameObject, background.transform);

            //playersTitle.horizontalAlignment = HorizontalAlignmentOptions.Center;
            //var backgroundImageToCopy = this.gameObject.transform.Find("BackgroundGroup").Find("PapperBackgroundImage").gameObject;
            //var backgroundImage = Instantiate(backgroundImageToCopy.gameObject, background.transform);
            //var backgroundImageRectangle = backgroundImage.GetComponent<RectTransform>();
            //backgroundImageRectangle.sizeDelta = lobbyWindowObjectRect.sizeDelta;

            // TBD test info
            var players = new List<string> { "Cat", "Dog", "Squirrel", "Rat" };
            var defaultTextMesh = title.GetComponentInChildren<TextMeshProUGUI>();
            var verticalContent = InstantiateDefaultGameObject(lobbyContent.transform);
            verticalContent.name = "VerticalContent";
            var vertical = verticalContent.AddComponent<VerticalLayoutGroupWorkaround>();
            var playersTitleObject = InstantiateDefaultGameObject(verticalContent.transform);
            var playersTitle = playersTitleObject.AddComponent<TextMeshProUGUI>();
            playersTitle.material = defaultTextMesh.material;
            playersTitle.color = defaultTextMesh.color;
            playersTitle.horizontalAlignment = HorizontalAlignmentOptions.Center;
            playersTitle.SetText("Players");

            foreach (var playerName in players)
            {
                var playerInfoObject = InstantiateDefaultGameObject(verticalContent.transform);
                playerInfoObject.AddComponent<HorizontalLayoutGroupWorkaround>();
                var ele = playerInfoObject.AddComponent<LayoutElement>();
                ele.preferredHeight = 60;
                var cz = playerInfoObject.AddComponent<ContentSizeFitterExtended>();
                cz.m_VerticalFit = ContentSizeFitterExtended.FitMode.PreferredSize;

                var player = InstantiateDefaultGameObject(playerInfoObject.transform);
                var playerTitle = player.AddComponent<TextMeshProUGUI>();
                playerTitle.material = defaultTextMesh.material;
                playerTitle.color = defaultTextMesh.color;
                playerTitle.SetText(playerName);
            }

            var characterControlTitleObject = InstantiateDefaultGameObject(verticalContent.transform);
            var characterControlTitle = characterControlTitleObject.AddComponent<TextMeshProUGUI>();
            characterControlTitle.material = defaultTextMesh.material;
            characterControlTitle.color = defaultTextMesh.color;
            characterControlTitle.horizontalAlignment = HorizontalAlignmentOptions.Center;
            characterControlTitle.SetText("Characters");


            var characterInfoLayout = InstantiateDefaultGameObject(verticalContent.transform);
            var h = characterInfoLayout.AddComponent<HorizontalLayoutGroupWorkaround>();
            h.childAlignment = TextAnchor.MiddleCenter;
            for (int i = 1; i <= 6; i++)
            {
                var specificCharInfo = InstantiateDefaultGameObject(characterInfoLayout.transform);
                specificCharInfo.AddComponent<VerticalLayoutGroupWorkaround>();

                var characterPortrait = InstantiateDefaultGameObject(specificCharInfo.transform);
                var portrait = characterPortrait.AddComponent<TextMeshProUGUI>();
                portrait.material = defaultTextMesh.material;
                portrait.color = defaultTextMesh.color;
                portrait.SetText($"portrait");

                var dropdownContainerObject = Instantiate(MainMenuSideBarPCViewPatches.DropdownPrefab.gameObject, specificCharInfo.transform);
                DestroyImmediate(dropdownContainerObject.GetComponent<SettingsEntityDropdownPCView>());
                DestroyImmediate(dropdownContainerObject.GetComponent<ContentSizeFitterExtended>());
                DestroyImmediate(dropdownContainerObject.GetComponent<VerticalLayoutGroupWorkaround>());
                DestroyImmediate(dropdownContainerObject.GetComponent<LayoutElement>());

                dropdownContainerObject.CleanupAllChildren(x => x.name != "Dropdown");
                var dropdownObject = dropdownContainerObject.transform.Find("Dropdown");
                var rect = dropdownObject.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(1, 0);
                rect.anchorMax = new Vector2(1, 0);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(lobbyWindowObjectRect.sizeDelta.x / 6, rect.sizeDelta.y);
                var dropdown = dropdownObject.GetComponent<TMP_Dropdown>();
                var templateRect = dropdownObject.Find("Template").GetComponent<RectTransform>();
                templateRect.sizeDelta = new Vector2(Math.Abs(templateRect.sizeDelta.x * 5), templateRect.sizeDelta.y);
                dropdown.ClearOptions();
                dropdown.AddOptions(players);
                dropdown.onValueChanged.AddListener(OnValueChanged);
            }
        }

        private void OnValueChanged(int x)
        {
            Logging.Logger.Info($"Value changed. Index={x}");
        }

        private GameObject InstantiateDefaultGameObject(Transform parent)
        {
            var obj = Instantiate(this.gameObject.transform.Find("Black").gameObject, parent);
            DestroyImmediate(obj.GetComponent<UnityEngine.UI.Image>());

            return obj;
        }

        private void SetupLoadSaveGamesLayout(GameObject hostItemContent)
        {
            var commonPCView = RootUIContext.Instance.m_CommonView as CommonPCView;
            // for some reason RootUIContext.Instance.m_CommonView is null after Loading Game -> Exiting to main menu
            // using cached copy which is always available on the first menu load
            var objToCopy = _cachedSaveLoadPCView ??= commonPCView?.m_SaveLoadPCView;
            SaveLoadPCView saveLoad = Instantiate(objToCopy, hostItemContent.transform);
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
