using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using DG.Tweening;
using Kingmaker.PubSubSystem;
using Kingmaker.UI;
using Kingmaker.UI.FullScreenUITypes;
using Kingmaker.UI.ServiceWindow;
using Owlcat.Runtime.UI.Controls.Button;
using Serilog;
using UnityEngine;
using WOTRMultiplayer.Abstractions.UI;
using WOTRMultiplayer.Abstractions.UI.Controllers.Menu;
using WOTRMultiplayer.Extensions;

namespace WOTRMultiplayer.UI.Menu
{
    public class MultiplayerWindow : FullScreenTabsWindow, IMultiplayerMenuWindow
    {
        private const string BaseLayoutName = "MultiplayerScreen";
        private const string SeparatorGameObjectName = "Separator";
        private const string MenuOverridesObjectName = "MenuEntity_NoOverrides";

        public override FullScreenUIType ActiveFullScreenUIType => (FullScreenUIType)555555;

        public Action OnDispose { get; set; }

        public ConcurrentQueue<Action> MainThreadQueue { get; } = new ConcurrentQueue<Action>();

        private List<DOTweenAnimation> _animations = [];

        private bool _isInitialized = false;

        private IHostMenuItemController _hostMenuController;
        private IJoinMenuItemController _joinMenuController;

        private readonly object _actionLock = new();

        public MultiplayerWindow()
        {
            // I assume this should be used to display menu items content,
            // but I have no idea how to make it work, so have to rely on my own `MenuItemController.MenuContent` implementation
            SubWindowsList = [];
        }

        public void AssignMenuItemControllers(IHostMenuItemController hostMenuItemController, IJoinMenuItemController joinMenuItemController)
        {
            _hostMenuController = hostMenuItemController;
            _joinMenuController = joinMenuItemController;
        }

        void Update()
        {
            while (MainThreadQueue.TryDequeue(out var action))
            {
                action();
            }
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
            StopAllCoroutines();
            base.OnHide();
        }

        public override void Show(bool state)
        {
            Log.Logger.Information("Show/Hide {windowTypeName}, State={state}", nameof(MultiplayerWindow), state);
            if (!state)
            {
                IMultiplayerMenuItemController controllerToAsk = _hostMenuController.IsActive ?
                    _hostMenuController
                    : _joinMenuController;
                OnDisposeMenuItem(controllerToAsk, this.Hide);
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
            _hostMenuController.Initialize(this, baseLayout, hostMenuItem);

            var joinMenuItem = CreateMenuItem(Screen.width * 0.66f, baseMenuItem, baseLayout.transform);
            _joinMenuController.Initialize(this, baseLayout, joinMenuItem);
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
            OnDisposeMenuItem(_joinMenuController, this.ActivateHostMenu);
        }

        private void OnJoinMenuItemClicked(object sender, EventArgs e)
        {
            OnDisposeMenuItem(_hostMenuController, this.ActivateJoinMenu);
        }

        private void OnDisposeMenuItem(IMultiplayerMenuItemController menuItemController, Action<MessageModalBase.ButtonType> onResult)
        {
            var confirmation = menuItemController.GetDeactivationConfirmation();
            if (confirmation != null)
            {
                var onModalClosed = confirmation.ModalType == MessageModalBase.ModalType.Dialog ? onResult : null;
                EventBus.RaiseEvent<IMessageModalUIHandler>(delegate (IMessageModalUIHandler w)
                {
                    w.HandleOpen(confirmation.Text, confirmation.ModalType, onModalClosed, null, null, null, null, null, null, 0, uint.MaxValue, null);
                }, true);
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
            _hostMenuController.Reset(false);
            _joinMenuController.Reset(false);
            base.Dispose();
        }
    }
}
