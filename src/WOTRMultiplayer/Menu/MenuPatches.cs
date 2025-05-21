using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using HarmonyLib;
using Kingmaker;
using Kingmaker.UI.Common;
using Kingmaker.UI.FullScreenUITypes;
using Kingmaker.UI.MVVM;
using Kingmaker.UI.MVVM._PCView.ContextMenu;
using Kingmaker.UI.MVVM._PCView.MainMenu;
using Kingmaker.UI.MVVM._PCView.SaveLoad;
using Kingmaker.UI.MVVM._VM.ContextMenu;
using Kingmaker.UI.ServiceWindow;
using Kingmaker.UI.ServiceWindow.Credits;
using Owlcat.Runtime.UI.Controls.Button;
using TMPro;
using UnityEngine;
using WOTRMultiplayer.Strings;

namespace WOTRMultiplayer.Menu
{
    [HarmonyPatch]
    public class MenuPatches
    {
        private static MultiplayerWindow _mainMenuMultiplayerWindow;
        private static SaveLoadPCView _saveLoadView;

        [HarmonyPatch(typeof(SaveLoadPCView), "DoInitialize")]
        [HarmonyPrefix]
        public static void SaveLoadPCView_BindViewImplementation_Prefix(SaveLoadPCView __instance)
        {
            Logging.Logger.Info("Applying");
            if (_saveLoadView == null)
            {
                _saveLoadView = __instance;
            }
        }

        [HarmonyPatch(typeof(MainMenuSideBarPCView), "BindViewImplementation")]
        [HarmonyPrefix]
        public static void MainMenuSideBarPCView_BindViewImplementation_Prefix(MainMenuSideBarPCView __instance)
        {
            Logging.Logger.Info("Applying");
            var menuButtons = __instance.transform.GetChild(0);
            var menuItemToCopy = menuButtons.GetChild(3).gameObject;
            var multiplayerMenu = UnityEngine.Object.Instantiate(menuItemToCopy, menuButtons.transform);
            multiplayerMenu.transform.SetSiblingIndex(3);
            var multiplayerMenuView = multiplayerMenu.GetComponent<ContextMenuEntityPCView>();
            var window = GetOrCreateMultiplayerWindow();
            var text = UIUtility.GetSaberBookFormat(StringsConst.MainMenu.MultiplayerMenu);
            var viewModel = new ContextMenuEntityVM(new ContextMenuCollectionEntity(UIUtility.GetSaberBookFormat(text), () => window.Show(true)));
            multiplayerMenuView.Bind(viewModel);
        }

        private static MultiplayerWindow GetOrCreateMultiplayerWindow()
        {
            if (_mainMenuMultiplayerWindow != null)
            {
                return _mainMenuMultiplayerWindow;
            }

            var copy = UnityEngine.Object.Instantiate(Game.Instance.UI.CreditsUI.gameObject, Game.Instance.UI.MainMenu.transform);
            var originalWindow = copy.GetComponent<CreditsUIWindow>();
            _mainMenuMultiplayerWindow = copy.AddComponent<MultiplayerWindow>();
            UnityEngine.Object.DestroyImmediate(originalWindow);
            _mainMenuMultiplayerWindow.Initialize();
            return _mainMenuMultiplayerWindow;
        }

        public class MenuItemController
        {
            private const string SelectedGameObjectName = "SelectedImage";
            private const string HoverGameObjectName = "HoverImage";

            private bool _isInitialized = false;
            private OwlcatButton _button => MenuItem.gameObject.GetComponent<OwlcatButton>();

            private GameObject _activeImage;
            private GameObject _hoverImage;

            public GameObject MenuItem { get; private set; }
            public GameObject MenuContent { get; private set; }

            public bool IsActive => MenuItem.gameObject.activeSelf;

            public event EventHandler OnClicked;

            public MenuItemController(GameObject menuItem, GameObject menuContent)
            {
                MenuItem = menuItem;
                MenuContent = menuContent;
            }

            public void Initialize()
            {
                if (_isInitialized)
                {
                    return;
                }

                _isInitialized = true;
                _button.OnHover.AddListener(OnHover);
                _button.OnLeftClick.AddListener(OnClickedInternal);
                _activeImage = this.MenuItem.transform.Find(SelectedGameObjectName).gameObject;
                _hoverImage = this.MenuItem.transform.Find(HoverGameObjectName).gameObject;

                Deactivate();
            }

            private void OnHover(bool state)
            {
                _hoverImage.SetActive(state);
            }

            public void Activate()
            {
                _activeImage.SetActive(true);
                MenuContent.SetActive(true);
            }

            public void Deactivate()
            {
                _activeImage.SetActive(false);
                MenuContent.SetActive(false);
            }

            private void OnClickedInternal()
            {
                OnClicked?.Invoke(this, EventArgs.Empty);
            }
        }

        public class MultiplayerWindow : FullScreenTabsWindow
        {
            private const string BaseLayoutName = "CreditsScreen";
            private const string SeparatorGameObjectName = "Separator";

            private const string MultiplayerMenuItemsObjectName = "MultiplayerMenuItems";
            private const string HostMenuItemContentObjectName = "HostMenuItemContent";
            private const string JoinMenuItemContentObjectName = "JoinMenuItemContent";
            private const string MenuOverridesObjectName = "MenuEntity_NoOverrides";

            public override FullScreenUIType ActiveFullScreenUIType => (FullScreenUIType)555555;

            private List<DOTweenAnimation> _animations = new List<DOTweenAnimation>();

            private bool _isInitialized = false;

            private MenuItemController _hostMenuController;
            private MenuItemController _joinMenuController;

            public MultiplayerWindow()
            {
                // I assume this should be used to display menu items content,
                // but I have no idea how to make it work, so have to rely on my own `MenuItemController.MenuContent` implementation
                SubWindowsList = System.Array.Empty<SubPair>();
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
                _animations = GetComponents<DOTweenAnimation>().ToList();
                var closeButton = this.GetComponentInChildren<Owlcat.Runtime.UI.Controls.Button.OwlcatButton>();
                closeButton.OnLeftClick.AddListener(OnCloseClicked);
            }

            public override void AppearAnimation()
            {
                base.AppearAnimation();
                this.gameObject.SetActive(true);
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
                this.gameObject.SetActive(false);
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
                Logging.Logger.Info($"Opening MP Window. State={state}");
                _hostMenuController.Activate();
                base.Show(state);
            }

            private void OnCloseClicked()
            {
                base.OnButtonClose();
            }

            private void SetupLayout()
            {
                var baseLayout = transform.Find(BaseLayoutName)?.gameObject;
                var (hostItemContent, joinItemContent) = SetupMenuItemsContentLayout(baseLayout);
                SetupMenuItemsLayout(baseLayout, hostItemContent, joinItemContent);
            }

            private (GameObject hostItemContent, GameObject joinItemContent) SetupMenuItemsContentLayout(GameObject baseLayout)
            {
                var hostItemContent = UnityEngine.Object.Instantiate(baseLayout, baseLayout.transform);
                hostItemContent.name = HostMenuItemContentObjectName;
                for (int i = hostItemContent.transform.childCount - 1; i >= 0; i--)
                {
                    if (i > 1)
                    {
                        continue;
                    }

                    UnityEngine.Object.DestroyImmediate(hostItemContent.transform.GetChild(i).gameObject);
                }

                var joinItemContent = UnityEngine.Object.Instantiate(baseLayout, baseLayout.transform);
                joinItemContent.name = JoinMenuItemContentObjectName;
                CleanupAllChildren(joinItemContent);
                return (hostItemContent, joinItemContent);
            }

            private void CleanupAllChildren(GameObject obj)
            {
                for (int i = obj.transform.childCount - 1; i >= 0; i--)
                {
                    UnityEngine.Object.DestroyImmediate(obj.transform.GetChild(i).gameObject);
                }
            }

            private void SetupMenuItemsLayout(GameObject baseLayout, GameObject hostItemContent, GameObject joinItemContent)
            {
                baseLayout.name = MultiplayerMenuItemsObjectName;
                CleanupBaseLayout(baseLayout);

                var baseMenuItem = SetupBaseMenuItem(baseLayout);
                _hostMenuController = SetupMenuController(StringsConst.MultiplayerWindow.HostMenuLabel, Screen.width * 0.33f, hostItemContent, baseMenuItem, baseLayout.transform);
                _joinMenuController = SetupMenuController(StringsConst.MultiplayerWindow.JoinMenuLabel, Screen.width * 0.66f, joinItemContent, baseMenuItem, baseLayout.transform);
                UnityEngine.Object.DestroyImmediate(baseMenuItem);

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

            private void CleanupBaseLayout(GameObject baseLayoutObject)
            {
                for (int i = baseLayoutObject.transform.childCount - 1; i >= 0; i--)
                {
                    var obj = baseLayoutObject.transform.GetChild(i).gameObject;
                    if (obj.name == HostMenuItemContentObjectName
                        || obj.name == JoinMenuItemContentObjectName
                        || obj.name == MenuOverridesObjectName)
                    {
                        continue;
                    }

                    UnityEngine.Object.DestroyImmediate(baseLayoutObject.transform.GetChild(i).gameObject);
                }
            }

            private GameObject SetupBaseMenuItem(GameObject baseLayoutObject)
            {
                var baseItem = baseLayoutObject.transform.GetChild(0).gameObject;

                UnityEngine.Object.DestroyImmediate(baseItem.GetComponent<OwlcatMultiButton>());
                baseItem.AddComponent<OwlcatButton>();

                var endSeparator = baseItem.transform.Find(SeparatorGameObjectName);
                UnityEngine.Object.DestroyImmediate(endSeparator.gameObject);

                return baseItem;
            }

            private MenuItemController SetupMenuController(string menuItemName, float positionX, GameObject menuContent, GameObject baseMenuItem, Transform parent)
            {
                var menuItem = UnityEngine.Object.Instantiate(baseMenuItem, parent);
                var position = new Vector3(positionX, menuItem.transform.position.y, menuItem.transform.position.z);
                menuItem.transform.SetPositionAndRotation(position, menuItem.transform.rotation);

                var label = menuItem.GetComponentInChildren<TextMeshProUGUI>();
                label.SetText(menuItemName);

                var controller = new MenuItemController(menuItem, menuContent);
                controller.Initialize();
                return controller;
            }
        }
    }
}
