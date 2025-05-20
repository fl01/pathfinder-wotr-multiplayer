using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using HarmonyLib;
using Kingmaker;
using Kingmaker.UI.Common;
using Kingmaker.UI.FullScreenUITypes;
using Kingmaker.UI.MVVM._PCView.ContextMenu;
using Kingmaker.UI.MVVM._PCView.MainMenu;
using Kingmaker.UI.MVVM._VM.ContextMenu;
using Kingmaker.UI.ServiceWindow;
using Kingmaker.UI.ServiceWindow.Credits;
using TMPro;
using UnityEngine;
using WOTRMultiplayer.Strings;

namespace WOTRMultiplayer.Menu
{
    [HarmonyPatch]
    public class MenuPatches
    {
        private static MultiplayerWindow _mainMenuMultiplayerWindow;

        [HarmonyPatch(typeof(MainMenuSideBarPCView), "BindViewImplementation")]
        [HarmonyPrefix]
        public static void MainMenuSideBarPCView_BindViewImplementation_Prefix(MainMenuSideBarPCView __instance)
        {
            Logging.Logger.Info("Applying");
            var menuButtons = __instance.transform.GetChild(0);
            var menuItemToCopy = menuButtons.GetChild(3).gameObject;
            var multiplayerMenu = Object.Instantiate(menuItemToCopy, menuButtons.transform);
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

            var copy = Object.Instantiate(Game.Instance.UI.CreditsUI.gameObject, Game.Instance.UI.MainMenu.transform);
            var originalWindow = copy.GetComponent<CreditsUIWindow>();
            _mainMenuMultiplayerWindow = copy.AddComponent<MultiplayerWindow>();
            Object.DestroyImmediate(originalWindow);
            var creditsScreen = copy.transform.Find("CreditsScreen")?.gameObject;
            SetupLayout(creditsScreen);
            _mainMenuMultiplayerWindow.Initialize();
            return _mainMenuMultiplayerWindow;
        }

        private static void SetupLayout(GameObject screen)
        {
            if (screen == null)
            {
                Logging.Logger.Error("MP Screen is missing");
                return;
            }

            for (int i = screen.transform.childCount - 1; i >= 0; i--)
            {
                if (i == 1)
                {
                    continue;
                }

                Object.DestroyImmediate(screen.transform.GetChild(i).gameObject);
            }

            var labelText = screen.transform.GetComponentInChildren<TextMeshProUGUI>();
            labelText.SetText(StringsConst.MultiplayerWindow.HostMenuLabel);
        }

        public class MultiplayerWindow : FullScreenTabsWindow
        {
            public override FullScreenUIType ActiveFullScreenUIType => (FullScreenUIType)555555;

            private List<DOTweenAnimation> _animations = new List<DOTweenAnimation>();

            private bool _isInitialized = false;

            public MultiplayerWindow()
            {
                SubWindowsList = System.Array.Empty<SubPair>();
            }

            public override void Initialize()
            {
                if (_isInitialized)
                {
                    Logging.Logger.Warning("Trying to initialize already initialized window");
                    return;
                }

                _isInitialized = true;
                base.Initialize();
                IsAnimated = true;
                var canvas = GetComponent<CanvasGroup>();
                canvas.alpha = 0f;
                _animations = GetComponents<DOTweenAnimation>().ToList();

                var closeButton = this.GetComponentInChildren<Owlcat.Runtime.UI.Controls.Button.OwlcatButton>();
                closeButton.OnLeftClick.AddListener(OnButtonClose);
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
                StopAllCoroutines();
                base.OnHide();
            }

            public override void Show(bool state)
            {
                Logging.Logger.Info($"Opening MP Window. State={state}");
                base.Show(state);
            }
        }
    }
}
