using System;
using System.Collections.Generic;
using DG.Tweening;
using Kingmaker.PubSubSystem;
using Kingmaker.UI;
using Kingmaker.UI.FullScreenUITypes;
using Kingmaker.UI.ServiceWindow;
using Microsoft.Extensions.Logging;
using Owlcat.Runtime.UI.Controls.Button;
using UnityEngine;
using WOTRMultiplayer.Abstractions.UI.Controllers.Menu;
using WOTRMultiplayer.Abstractions.UI.Windows;
using WOTRMultiplayer.Extensions;

namespace WOTRMultiplayer.UI.Menu
{
    public class MultiplayerWindow : FullScreenTabsWindow, IMultiplayerWindow
    {
        private const string BaseLayoutName = "MultiplayerScreen";
        private const string SeparatorGameObjectName = "Separator";
        private const string MenuOverridesObjectName = "MenuEntity_NoOverrides";

        public override FullScreenUIType ActiveFullScreenUIType => (FullScreenUIType)555555;
        private List<DOTweenAnimation> _animations = [];

        private bool _isInitialized = false;

        private IHostMenuItemController _hostMenuController;
        private IJoinMenuItemController _joinMenuController;
        private ILogger<MultiplayerWindow> _logger;
        private readonly object _actionLock = new();

        public MultiplayerWindow()
        {
            // I assume this should be used to display menu items content,
            // but I have no idea how to make it work, so have to rely on my own `MenuItemController.MenuContent` implementation
            SubWindowsList = [];
        }

        public void SetLogger(ILogger<MultiplayerWindow> logger)
        {
            _logger = logger;
        }

        public void AssignMenuItemControllers(IHostMenuItemController hostMenuItemController, IJoinMenuItemController joinMenuItemController)
        {
            _hostMenuController = hostMenuItemController;
            _joinMenuController = joinMenuItemController;
        }

        public override void Initialize()
        {
            if (_isInitialized)
            {
                _logger.LogWarning("Trying to initialize already initialized window");
                return;
            }

            _isInitialized = true;
            _logger.LogInformation("Initalizing");

            Main.Multiplayer.Factory.StoreDefaultGameObject(gameObject.transform.Find("Black").gameObject);

            SetupLayout();

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

        public override void Show(bool state)
        {
            _logger.LogInformation("Show/Hide {windowTypeName}, State={state}", nameof(MultiplayerWindow), state);
            if (!state)
            {
                IMultiplayerMenuItemController controllerToAsk = _hostMenuController.IsActive ?
                    _hostMenuController
                    : _joinMenuController;
                OnSelectMenuItem(controllerToAsk, Hide);
                return;
            }

            _hostMenuController.Activate();
            base.Show(state);
        }

        private void Hide(MessageModalBase.ButtonType button)
        {
            if (button == MessageModalBase.ButtonType.Yes)
            {
                _joinMenuController.Deactivate();
                _hostMenuController.Deactivate();
                base.Show(false);
            }
        }

        public void OnCloseClicked()
        {
            OnButtonClose();
        }

        private void SetupLayout()
        {
            var baseLayout = transform.Find("CreditsScreen")?.gameObject;

            var baseMenuItem = SetupBaseMenuItem(baseLayout);
            var hostMenuItem = CreateMenuItem(Screen.width * 0.33f, baseMenuItem, baseLayout.transform);
            _hostMenuController.Initialize(baseLayout, hostMenuItem);

            var joinMenuItem = CreateMenuItem(Screen.width * 0.66f, baseMenuItem, baseLayout.transform);
            _joinMenuController.Initialize(baseLayout, joinMenuItem);
            DestroyImmediate(baseMenuItem);

            _hostMenuController.OnClicked = OnHostMenuItemClicked;
            _joinMenuController.OnClicked = OnJoinMenuItemClicked;
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
            OnSelectMenuItem(_joinMenuController, ActivateHostMenu);
        }

        private void OnJoinMenuItemClicked(object sender, EventArgs e)
        {
            OnSelectMenuItem(_hostMenuController, ActivateJoinMenu);
        }

        private void OnSelectMenuItem(IMultiplayerMenuItemController menuItemController, Action<MessageModalBase.ButtonType> onResult)
        {
            var confirmation = menuItemController.GetDeactivationConfirmation();
            if (confirmation != null)
            {
                _logger.LogInformation("Deactivation confirmation required");

                var onModalClosed = confirmation.ModalType == MessageModalBase.ModalType.Dialog ? onResult : null;
                EventBus.RaiseEvent<IMessageModalUIHandler>(window =>
                {
                    window.HandleOpen(confirmation.Text, confirmation.ModalType, onModalClosed);
                });
                return;
            }

            onResult(MessageModalBase.ButtonType.Yes);
        }

        private void ActivateJoinMenu(MessageModalBase.ButtonType button)
        {
            if (button == MessageModalBase.ButtonType.Yes)
            {
                _hostMenuController.Deactivate();
                _joinMenuController.Activate();
            }
        }

        private void ActivateHostMenu(MessageModalBase.ButtonType button)
        {
            if (button == MessageModalBase.ButtonType.Yes)
            {
                _hostMenuController.Activate();
                _joinMenuController.Deactivate();
            }
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

        public override void Dispose()
        {
            _logger.LogInformation("Dispose");
            _hostMenuController.Dispose();
            _joinMenuController.Dispose();
            base.Dispose();
        }
    }
}
